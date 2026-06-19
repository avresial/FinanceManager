using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// Tests for the range/bucket caching layer added in issue #456 on top of the
/// point-read cache from issue #455. All tests use a real in-process HybridCache
/// and a Moq inner repository so cache hits/misses are observable via call counts.
/// </summary>
[Trait("Category", "Unit")]
public class CachedAccountEntryRepositoryRangeTests
{
    private const int AccountId = 7;
    private const int UserId = 42;

    // Fixed months used in bucket-boundary tests; stay well inside the 12-month horizon.
    private static readonly DateTime Jan = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Feb = new(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Mar = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static HybridCache BuildCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static IAccountUserResolver ResolverFor(int accountId, int? userId)
    {
        var m = new Mock<IAccountUserResolver>();
        m.Setup(r => r.GetUserId(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(userId);
        return m.Object;
    }

    private static CurrencyAccountEntry Entry(int entryId, DateTime date, decimal value = 100m) =>
        new(AccountId, entryId, date, value, value);

    // Long TTLs prevent expiry during tests; a large horizon (120 months) keeps the fixed 2025 test
    // dates always inside the bucket-cache path regardless of when the tests are run.
    private static readonly EntryRangeCacheOptions DefaultOptions = new()
    {
        HorizonMonths = 120,
        OpenMonthTtl = TimeSpan.FromHours(1),
        ElapsedMonthTtl = TimeSpan.FromHours(2)
    };

    private static readonly EntryRangeCacheOptions TwelveMonthOptions = new()
    {
        HorizonMonths = 12,
        OpenMonthTtl = TimeSpan.FromHours(1),
        ElapsedMonthTtl = TimeSpan.FromHours(2)
    };

    private static CachedAccountEntryRepository<CurrencyAccountEntry> CreateSut(
        IAccountEntryRepository<CurrencyAccountEntry> inner,
        HybridCache cache,
        EntryRangeCacheOptions? options = null,
        IAccountUserResolver? resolver = null,
        ICacheInvalidator? invalidator = null) =>
        new(inner,
            resolver ?? ResolverFor(AccountId, UserId),
            invalidator ?? Mock.Of<ICacheInvalidator>(),
            cache,
            options ?? DefaultOptions);

    // ── MonthBucket math ─────────────────────────────────────────────────────────

    [Fact]
    public void MonthBucket_MonthStart_TruncatesToFirstOfMonth()
    {
        var result = MonthBucket.MonthStart(new DateTime(2025, 3, 15, 12, 30, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void MonthBucket_MonthEndInclusive_IsLastTickOfMonth()
    {
        var jan = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = MonthBucket.MonthEndInclusive(jan);
        // Jan 31 23:59:59.9999999 UTC
        var expected = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MonthBucket_TouchedMonths_ReturnsSingleMonthForIntraMonthRange()
    {
        var months = MonthBucket.TouchedMonths(
            new DateTime(2025, 3, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc)).ToList();

        Assert.Single(months);
        Assert.Equal(Mar, months[0]);
    }

    [Fact]
    public void MonthBucket_TouchedMonths_ReturnsAllMonthsInRange()
    {
        var months = MonthBucket.TouchedMonths(Jan, Mar).ToList();
        Assert.Equal([Jan, Feb, Mar], months);
    }

    [Fact]
    public void MonthBucket_HorizonStart_IsFirstDayOfMonthNMonthsAgo()
    {
        var now = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var horizon = MonthBucket.HorizonStart(12, now);
        Assert.Equal(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), horizon);
    }

    // ── Inflate-on-miss: partial request seeds the whole bucket ──────────────────

    [Fact]
    public async Task Get_PartialMonthRequest_InflatesInnerCallToFullMonth()
    {
        var jan10 = Jan.AddDays(9);
        var jan20 = Jan.AddDays(19);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(2, jan20), Entry(1, jan10) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache());
        var ct = TestContext.Current.CancellationToken;

        var result = await sut.Get(AccountId, jan10, jan20).ToListAsync(ct);

        // Inner was called with FULL month bounds, not the requested slice.
        inner.Verify(r => r.Get(AccountId, Jan, janEnd), Times.Once);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Get_SecondPartialMonthRequest_HitsCache_InnerCalledOnce()
    {
        // First call Jan 10–20 seeds the whole January bucket.
        // Second call Jan 5–25 is a full cache hit — inner NOT called again.
        var jan5 = Jan.AddDays(4);
        var jan10 = Jan.AddDays(9);
        var jan20 = Jan.AddDays(19);
        var jan25 = Jan.AddDays(24);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[]
             {
                 Entry(4, jan25), Entry(2, jan20), Entry(1, jan10), Entry(3, jan5)
             }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        var first = await sut.Get(AccountId, jan10, jan20).ToListAsync(ct);
        var second = await sut.Get(AccountId, jan5, jan25).ToListAsync(ct);

        inner.Verify(r => r.Get(AccountId, Jan, janEnd), Times.Once);
        Assert.Equal(2, first.Count);
        Assert.Equal(4, second.Count);
    }

    // ── Compose across buckets ────────────────────────────────────────────────────

    [Fact]
    public async Task Get_SpanningTwoMonths_ComposesAndSortsNewestFirst()
    {
        var jan15 = Jan.AddDays(14);
        var feb10 = Feb.AddDays(9);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);
        var febEnd = MonthBucket.MonthEndInclusive(Feb);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(1, jan15) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(AccountId, Feb, febEnd))
             .Returns(new[] { Entry(2, feb10) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache());
        var result = await sut.Get(AccountId, jan15, feb10).ToListAsync(ct);

        Assert.Equal(2, result.Count);
        // Newest-first: Feb entry (id=2) before Jan entry (id=1)
        Assert.Equal(2, result[0].EntryId);
        Assert.Equal(1, result[1].EntryId);
    }

    [Fact]
    public async Task Get_OverlappingRequests_SecondFetchOnlyMissingMonth()
    {
        // First request seeds Jan + Feb; second request needs Feb + Mar → only March hits DB.
        var jan15 = Jan.AddDays(14);
        var mar20 = Mar.AddDays(19);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);
        var febEnd = MonthBucket.MonthEndInclusive(Feb);
        var marEnd = MonthBucket.MonthEndInclusive(Mar);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(1, jan15) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(AccountId, Feb, febEnd))
             .Returns(new[] { Entry(2, Feb.AddDays(10)) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(AccountId, Mar, marEnd))
             .Returns(new[] { Entry(3, Mar.AddDays(5)) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        await sut.Get(AccountId, jan15, Feb.AddDays(28)).ToListAsync(ct);   // seeds Jan + Feb
        var result = await sut.Get(AccountId, Feb, mar20).ToListAsync(ct);  // Feb=hit, Mar=miss

        inner.Verify(r => r.Get(AccountId, Jan, janEnd), Times.Once);
        inner.Verify(r => r.Get(AccountId, Feb, febEnd), Times.Once);
        inner.Verify(r => r.Get(AccountId, Mar, marEnd), Times.Once);
        Assert.Equal(2, result.Count);
    }

    // ── Empty months cached as empty (no re-query) ────────────────────────────────

    [Fact]
    public async Task Get_EmptyMonth_CachedAsEmpty_InnerNotCalledAgain()
    {
        var janEnd = MonthBucket.MonthEndInclusive(Jan);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        var first = await sut.Get(AccountId, Jan, janEnd).ToListAsync(ct);
        var second = await sut.Get(AccountId, Jan, janEnd).ToListAsync(ct);

        inner.Verify(r => r.Get(AccountId, Jan, janEnd), Times.Once);
        Assert.Empty(first);
        Assert.Empty(second);
    }

    // ── Half-open boundary: entry on the 1st is NOT duplicated ───────────────────

    [Fact]
    public async Task Get_EntryOnFirstOfMonth_NotDuplicated_WhenBothMonthsQueried()
    {
        // An entry dated Feb 1 lives in the February bucket.
        // Requesting Jan through Feb must return it exactly once.
        var feb1Entry = Entry(10, Feb);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);
        var febEnd = MonthBucket.MonthEndInclusive(Feb);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());
        inner.Setup(r => r.Get(AccountId, Feb, febEnd))
             .Returns(new[] { feb1Entry }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache());
        var result = await sut.Get(AccountId, Jan, Feb.AddDays(27)).ToListAsync(ct);

        Assert.Single(result);
        Assert.Equal(10, result[0].EntryId);
    }

    [Fact]
    public async Task Get_EntryOnLastDayOfMonth_IncludedInRequest()
    {
        var jan31 = new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(5, jan31) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache());
        var result = await sut.Get(AccountId, Jan, janEnd).ToListAsync(ct);

        Assert.Single(result);
        Assert.Equal(5, result[0].EntryId);
    }

    // ── Horizon split ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_CrossingHorizon_SplitsIntoUncachedOlderAndBucketedRecent()
    {
        // Pin the "recent" window to exactly the first month inside the 12-month horizon so
        // GetBucketedEntries inflates exactly one bucket — otherwise we'd need stubs for every
        // month between horizonStart and endDate, which makes the test fragile.
        var horizon = MonthBucket.HorizonStart(12, DateTime.UtcNow);
        var horizonMonthEnd = MonthBucket.MonthEndInclusive(horizon);
        var recentDate = horizon.AddDays(5);   // a date in the first horizon month

        var oldDate = horizon.AddMonths(-2);   // 2 months before horizon: definitely uncached
        var olderEnd = horizon.AddTicks(-1);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, oldDate, olderEnd))
             .Returns(new[] { Entry(1, oldDate) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(AccountId, horizon, horizonMonthEnd))
             .Returns(new[] { Entry(2, recentDate) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache(), options: TwelveMonthOptions);
        var result = await sut.Get(AccountId, oldDate, recentDate).ToListAsync(ct);

        // Older portion passed straight through; recent portion inflated to full month.
        inner.Verify(r => r.Get(AccountId, oldDate, olderEnd), Times.Once);
        inner.Verify(r => r.Get(AccountId, horizon, horizonMonthEnd), Times.Once);
        Assert.Equal(2, result.Count);
        // Newest-first: recent (id=2) before old (id=1).
        Assert.Equal(2, result[0].EntryId);
        Assert.Equal(1, result[1].EntryId);
    }

    [Fact]
    public async Task Get_EntirelyBeforeHorizon_BypassesBucketCacheCompletely()
    {
        // 14-month-old range — beyond any 12-month horizon.
        var horizon = MonthBucket.HorizonStart(12, DateTime.UtcNow);
        var oldStart = horizon.AddMonths(-2);
        var oldEnd = horizon.AddTicks(-1);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, oldStart, oldEnd))
             .Returns(new[] { Entry(1, oldStart) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache, options: TwelveMonthOptions);

        await sut.Get(AccountId, oldStart, oldEnd).ToListAsync(ct);
        await sut.Get(AccountId, oldStart, oldEnd).ToListAsync(ct);

        // Both calls pass through — no bucket caching for old data.
        inner.Verify(r => r.Get(AccountId, oldStart, oldEnd), Times.Exactly(2));
    }

    // ── GetEntriesWithMinimumCount routes through the cached Get ─────────────────

    [Fact]
    public async Task GetEntriesWithMinimumCount_ZeroMinimum_UsesRangeBucketCache()
    {
        var janEnd = MonthBucket.MonthEndInclusive(Jan);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(1, Jan.AddDays(10)) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        var (entries, effectiveStart) = await sut.GetEntriesWithMinimumCount(AccountId, Jan, janEnd);

        Assert.Single(entries);
        Assert.Equal(Jan, effectiveStart);

        // Second call: full cache hit.
        await sut.GetEntriesWithMinimumCount(AccountId, Jan, janEnd);
        inner.Verify(r => r.Get(AccountId, Jan, janEnd), Times.Once);
    }

    [Fact]
    public async Task GetEntriesWithMinimumCount_UsesCachedPostingDatesAndBucketedGet()
    {
        var jan5 = Jan.AddDays(4);
        var jan10 = Jan.AddDays(9);
        var jan20 = Jan.AddDays(19);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetPostingDates(AccountId))
             .ReturnsAsync([jan20, jan10, jan5]);
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(3, jan20), Entry(2, jan10), Entry(1, jan5) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache);

        var (entries, effectiveStart) = await sut.GetEntriesWithMinimumCount(
            AccountId, jan5, janEnd, minimumEntryCount: 2);

        Assert.Equal(3, entries.Count);
        Assert.Equal(jan5, effectiveStart);
        // Inner Get was inflated to full month.
        inner.Verify(r => r.Get(AccountId, Jan, janEnd), Times.Once);
        // Inner GetEntriesWithMinimumCount must NOT be called (decorator handles it).
        inner.Verify(r => r.GetEntriesWithMinimumCount(
            It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
    }

    // ── GetRange (multi-account) ───────────────────────────────────────────────────

    [Fact]
    public async Task GetRange_MultiAccount_ComposesBothAccountsFromBuckets()
    {
        const int AccountId2 = 8;
        const int UserId2 = 43;

        var jan10 = Jan.AddDays(9);
        var jan20 = Jan.AddDays(19);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);

        var resolver = new Mock<IAccountUserResolver>();
        resolver.Setup(r => r.GetUserId(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        resolver.Setup(r => r.GetUserId(AccountId2, It.IsAny<CancellationToken>())).ReturnsAsync(UserId2);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(1, jan20) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(AccountId2, Jan, janEnd))
             .Returns(new[] { new CurrencyAccountEntry(AccountId2, 2, jan10, 200m, 200m) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache(), resolver: resolver.Object);

        var result = await sut.GetRange(
            [AccountId, AccountId2], Jan, janEnd);

        Assert.Equal(2, result.Count);
        // Newest-first across both accounts.
        Assert.Equal(1, result[0].EntryId);
        Assert.Equal(2, result[1].EntryId);
    }

    [Fact]
    public async Task GetRange_SecondCall_ServedFromBuckets_InnerNotCalledAgain()
    {
        const int AccountId2 = 8;
        const int UserId2 = 43;
        var janEnd = MonthBucket.MonthEndInclusive(Jan);

        var resolver = new Mock<IAccountUserResolver>();
        resolver.Setup(r => r.GetUserId(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        resolver.Setup(r => r.GetUserId(AccountId2, It.IsAny<CancellationToken>())).ReturnsAsync(UserId2);

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(1, Jan.AddDays(5)) }.ToAsyncEnumerable());
        inner.Setup(r => r.Get(AccountId2, Jan, janEnd))
             .Returns(new[] { new CurrencyAccountEntry(AccountId2, 2, Jan.AddDays(10), 50m, 50m) }.ToAsyncEnumerable());

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache, resolver: resolver.Object);

        await sut.GetRange([AccountId, AccountId2], Jan, janEnd);
        await sut.GetRange([AccountId, AccountId2], Jan, janEnd);

        inner.Verify(r => r.Get(AccountId, Jan, janEnd), Times.Once);
        inner.Verify(r => r.Get(AccountId2, Jan, janEnd), Times.Once);
    }

    // ── Invalidation busts range buckets ──────────────────────────────────────────

    [Fact]
    public async Task Write_BustsBucketedRange_NextReadHitsInnerAgain()
    {
        var jan5 = Jan.AddDays(4);
        var jan15 = Jan.AddDays(14);
        var janEnd = MonthBucket.MonthEndInclusive(Jan);
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.SetupSequence(r => r.Get(AccountId, Jan, janEnd))
             .Returns(new[] { Entry(1, jan5, 100m) }.ToAsyncEnumerable())
             .Returns(new[] { Entry(2, jan15, 200m), Entry(1, jan5, 200m) }.ToAsyncEnumerable());
        inner.Setup(r => r.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<bool>())).ReturnsAsync(true);

        var cache = BuildCache();
        var sut = CreateSut(inner.Object, cache, invalidator: new CacheInvalidator(cache));

        var before = await sut.Get(AccountId, Jan, janEnd).ToListAsync(ct); // miss → cached
        await sut.Add(Entry(2, jan15, 200m));                               // write busts global:u42
        var after = await sut.Get(AccountId, Jan, janEnd).ToListAsync(ct);  // miss again

        Assert.Single(before);
        Assert.Equal(2, after.Count);
        Assert.Equal(200m, after[0].Value);
    }

    // ── Unresolvable owner falls back to inner ────────────────────────────────────

    [Fact]
    public async Task Get_WhenOwnerUnresolved_PassesThroughUncached()
    {
        var ct = TestContext.Current.CancellationToken;

        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Get(AccountId, Jan, Feb))
             .Returns(new[] { Entry(1, Jan.AddDays(5)) }.ToAsyncEnumerable());

        var sut = CreateSut(inner.Object, BuildCache(), resolver: ResolverFor(AccountId, null));

        await sut.Get(AccountId, Jan, Feb).ToListAsync(ct);
        await sut.Get(AccountId, Jan, Feb).ToListAsync(ct);

        // Both calls fall through — no caching without a resolvable userId.
        inner.Verify(r => r.Get(AccountId, Jan, Feb), Times.Exactly(2));
    }
}