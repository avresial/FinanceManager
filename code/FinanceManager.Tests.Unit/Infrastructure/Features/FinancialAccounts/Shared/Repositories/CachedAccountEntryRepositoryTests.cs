using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

[Trait("Category", "Unit")]
public class CachedAccountEntryRepositoryTests
{
    private const int _accountId = 7;
    private const int _userId = 42;
    private static readonly DateTime _date = new(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static IAccountUserResolver ResolverFor(int accountId, int? userId)
    {
        var resolver = new Mock<IAccountUserResolver>();
        resolver.Setup(r => r.GetUserId(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(userId);
        return resolver.Object;
    }

    private static CurrencyAccountEntry Entry(int entryId = 1, decimal value = 100m) =>
        new(_accountId, entryId, _date, value, value);

    private static CachedAccountEntryRepository<CurrencyAccountEntry> CreateSut(
        IAccountEntryRepository<CurrencyAccountEntry> inner,
        IAccountUserResolver resolver,
        ICacheInvalidator invalidator,
        HybridCache cache) => new(inner, resolver, invalidator, cache, new EntryRangeCacheOptions());

    // -------------------------------------------------------------------------
    // Cache hits avoid the inner repository call
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetYoungest_SecondCall_ServedFromCache_InnerCalledOnce()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetYoungest(_accountId)).ReturnsAsync(Entry(5, 250m));
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), Mock.Of<ICacheInvalidator>(), CreateCache());

        var first = await sut.GetYoungest(_accountId);
        var second = await sut.GetYoungest(_accountId);

        Assert.Equal(250m, first!.Value);
        Assert.Equal(5, second!.EntryId);
        inner.Verify(r => r.GetYoungest(_accountId), Times.Once);
    }

    [Fact]
    public async Task GetOldest_SecondCall_ServedFromCache_InnerCalledOnce()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetOldest(_accountId)).ReturnsAsync(Entry(1, 10m));
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), Mock.Of<ICacheInvalidator>(), CreateCache());

        await sut.GetOldest(_accountId);
        await sut.GetOldest(_accountId);

        inner.Verify(r => r.GetOldest(_accountId), Times.Once);
    }

    [Fact]
    public async Task GetCount_SecondCall_ServedFromCache_InnerCalledOnce()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetCount(_accountId)).ReturnsAsync(3);
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), Mock.Of<ICacheInvalidator>(), CreateCache());

        var first = await sut.GetCount(_accountId);
        var second = await sut.GetCount(_accountId);

        Assert.Equal(3, first);
        Assert.Equal(3, second);
        inner.Verify(r => r.GetCount(_accountId), Times.Once);
    }

    [Fact]
    public async Task GetPostingDates_SecondCall_ServedFromCache_InnerCalledOnce()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetPostingDates(_accountId)).ReturnsAsync([_date]);
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), Mock.Of<ICacheInvalidator>(), CreateCache());

        await sut.GetPostingDates(_accountId);
        var second = await sut.GetPostingDates(_accountId);

        Assert.Equal([_date], second);
        inner.Verify(r => r.GetPostingDates(_accountId), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DateBoundaries_RepeatedReads_PassThroughWithExactTimestamp(bool older)
    {
        var date = _date.AddHours(12).AddTicks(123);
        var adjacentDate = date.AddTicks(1);
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetNextOlder(_accountId, date)).ReturnsAsync(Entry(1));
        inner.Setup(r => r.GetNextYounger(_accountId, date)).ReturnsAsync(Entry(1));
        inner.Setup(r => r.GetNextOlder(_accountId, adjacentDate)).ReturnsAsync(Entry(2));
        inner.Setup(r => r.GetNextYounger(_accountId, adjacentDate)).ReturnsAsync(Entry(2));
        var sut = CreateSut(inner.Object, Mock.Of<IAccountUserResolver>(MockBehavior.Strict),
            Mock.Of<ICacheInvalidator>(MockBehavior.Strict), CreateCache());

        Task<CurrencyAccountEntry?> Read(DateTime boundary) => older
            ? sut.GetNextOlder(_accountId, boundary)
            : sut.GetNextYounger(_accountId, boundary);

        Assert.Equal(1, (await Read(date))!.EntryId);
        Assert.Equal(1, (await Read(date))!.EntryId);
        Assert.Equal(2, (await Read(adjacentDate))!.EntryId);

        if (older)
        {
            inner.Verify(r => r.GetNextOlder(_accountId, date), Times.Exactly(2));
            inner.Verify(r => r.GetNextOlder(_accountId, adjacentDate), Times.Once);
        }
        else
        {
            inner.Verify(r => r.GetNextYounger(_accountId, date), Times.Exactly(2));
            inner.Verify(r => r.GetNextYounger(_accountId, adjacentDate), Times.Once);
        }
        inner.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, 25)]
    [InlineData(true, 50)]
    [InlineData(false, 25)]
    public async Task EntryPage_RepeatedReads_PassThroughWithExactArguments(bool olderThenDate, int count)
    {
        var date = _date.AddHours(12).AddTicks(123);
        var adjacentDate = date.AddTicks(1);
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.SetupSequence(r => r.Get(_accountId, date, count, olderThenDate))
            .ReturnsAsync([Entry(2), Entry(1)])
            .ReturnsAsync([Entry(3), Entry(2)]);
        inner.Setup(r => r.Get(_accountId, adjacentDate, count, olderThenDate)).ReturnsAsync([Entry(4)]);
        var sut = CreateSut(inner.Object, Mock.Of<IAccountUserResolver>(MockBehavior.Strict),
            Mock.Of<ICacheInvalidator>(MockBehavior.Strict), CreateCache());

        Assert.Equal([2, 1], (await sut.Get(_accountId, date, count, olderThenDate)).Select(entry => entry.EntryId));
        Assert.Equal([3, 2], (await sut.Get(_accountId, date, count, olderThenDate)).Select(entry => entry.EntryId));
        Assert.Equal(4, Assert.Single(await sut.Get(_accountId, adjacentDate, count, olderThenDate)).EntryId);
        inner.Verify(r => r.Get(_accountId, date, count, olderThenDate), Times.Exactly(2));
        inner.Verify(r => r.Get(_accountId, adjacentDate, count, olderThenDate), Times.Once);
        inner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetYoungest_WhenOwnerUnresolved_BypassesCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetYoungest(_accountId)).ReturnsAsync(Entry());
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, null), Mock.Of<ICacheInvalidator>(), CreateCache());

        await sut.GetYoungest(_accountId);
        await sut.GetYoungest(_accountId);

        inner.Verify(r => r.GetYoungest(_accountId), Times.Exactly(2));
    }

    // -------------------------------------------------------------------------
    // Every write path invalidates the owning user's cache
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Add_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Add(
                It.IsAny<CurrencyAccountEntry>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), invalidator.Object, CreateCache());

        await sut.Add(Entry(), recalculate: true, cancellationToken: TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddBatch_InvalidatesOwnersCache_OncePerUser()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Add(
                It.IsAny<IEnumerable<CurrencyAccountEntry>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), invalidator.Object, CreateCache());

        await sut.Add([Entry(1), Entry(2), Entry(3)], recalculate: true, cancellationToken: TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Update(It.IsAny<CurrencyAccountEntry>())).ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), invalidator.Object, CreateCache());

        await sut.Update(Entry());

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEntry_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Delete(_accountId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), invalidator.Object, CreateCache());

        await sut.Delete(_accountId, 1, TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccount_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Delete(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), invalidator.Object, CreateCache());

        await sut.Delete(_accountId, TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddLabel_ResolvesOwnerFromEntry_AndInvalidates()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.AddLabel(11, 3)).ReturnsAsync(true);
        inner.Setup(r => r.GetByIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entry(11)]);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), invalidator.Object, CreateCache());

        await sut.AddLabel(11, 3);

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddLabels_ResolvesOwnersFromEntries_AndInvalidates()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.AddLabels(It.IsAny<IEnumerable<(int, int)>>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        inner.Setup(r => r.GetByIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entry(11), Entry(12)]);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), invalidator.Object, CreateCache());

        await sut.AddLabels([(11, 3), (12, 3)], TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // End-to-end: a write busts the cached point reads (real cache + invalidator)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_BustsCachedPointReads_NextReadHitsInnerAgain()
    {
        var cache = CreateCache();
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.SetupSequence(r => r.GetYoungest(_accountId))
            .ReturnsAsync(Entry(1, 100m))
            .ReturnsAsync(Entry(1, 175m));
        inner.Setup(r => r.Update(It.IsAny<CurrencyAccountEntry>())).ReturnsAsync(true);
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), new CacheInvalidator(cache), cache);

        var before = await sut.GetYoungest(_accountId);   // miss → 100, cached
        await sut.GetYoungest(_accountId);                // hit
        await sut.Update(Entry(1, 175m));                // busts global:u{_userId}
        var after = await sut.GetYoungest(_accountId);    // miss again → 175

        Assert.Equal(100m, before!.Value);
        Assert.Equal(175m, after!.Value);
        inner.Verify(r => r.GetYoungest(_accountId), Times.Exactly(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecalculateValues_BustsCachedPointReads_NextReadHitsInnerAgain(bool fromEntry)
    {
        const int entryId = 1;
        var cache = CreateCache();
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.SetupSequence(r => r.GetYoungest(_accountId))
            .ReturnsAsync(Entry(entryId, 100m))
            .ReturnsAsync(Entry(entryId, 175m));
        var sut = CreateSut(inner.Object, ResolverFor(_accountId, _userId), new CacheInvalidator(cache), cache);

        var before = (await sut.GetYoungest(_accountId))!;
        await sut.GetYoungest(_accountId);

        if (fromEntry)
            await sut.RecalculateValues(_accountId, entryId, TestContext.Current.CancellationToken);
        else
            await sut.RecalculateValues(_accountId, TestContext.Current.CancellationToken);

        var after = (await sut.GetYoungest(_accountId))!;

        Assert.Equal(100m, before.Value);
        Assert.Equal(175m, after.Value);
        inner.Verify(r => r.GetYoungest(_accountId), Times.Exactly(2));
    }
}