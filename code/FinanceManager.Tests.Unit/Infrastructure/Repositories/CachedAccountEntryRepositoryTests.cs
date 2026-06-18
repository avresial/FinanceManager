using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

[Trait("Category", "Unit")]
public class CachedAccountEntryRepositoryTests
{
    private const int AccountId = 7;
    private const int UserId = 42;
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
        new(AccountId, entryId, _date, value, value);

    private static CachedAccountEntryRepository<CurrencyAccountEntry> CreateSut(
        IAccountEntryRepository<CurrencyAccountEntry> inner,
        IAccountUserResolver resolver,
        ICacheInvalidator invalidator,
        HybridCache cache) => new(inner, resolver, invalidator, cache);

    // -------------------------------------------------------------------------
    // Cache hits avoid the inner repository call
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetYoungest_SecondCall_ServedFromCache_InnerCalledOnce()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetYoungest(AccountId)).ReturnsAsync(Entry(5, 250m));
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), Mock.Of<ICacheInvalidator>(), CreateCache());

        var first = await sut.GetYoungest(AccountId);
        var second = await sut.GetYoungest(AccountId);

        Assert.Equal(250m, first!.Value);
        Assert.Equal(5, second!.EntryId);
        inner.Verify(r => r.GetYoungest(AccountId), Times.Once);
    }

    [Fact]
    public async Task GetOldest_SecondCall_ServedFromCache_InnerCalledOnce()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetOldest(AccountId)).ReturnsAsync(Entry(1, 10m));
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), Mock.Of<ICacheInvalidator>(), CreateCache());

        await sut.GetOldest(AccountId);
        await sut.GetOldest(AccountId);

        inner.Verify(r => r.GetOldest(AccountId), Times.Once);
    }

    [Fact]
    public async Task GetCount_SecondCall_ServedFromCache_InnerCalledOnce()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetCount(AccountId)).ReturnsAsync(3);
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), Mock.Of<ICacheInvalidator>(), CreateCache());

        var first = await sut.GetCount(AccountId);
        var second = await sut.GetCount(AccountId);

        Assert.Equal(3, first);
        Assert.Equal(3, second);
        inner.Verify(r => r.GetCount(AccountId), Times.Once);
    }

    [Fact]
    public async Task GetPostingDates_SecondCall_ServedFromCache_InnerCalledOnce()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetPostingDates(AccountId)).ReturnsAsync([_date]);
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), Mock.Of<ICacheInvalidator>(), CreateCache());

        await sut.GetPostingDates(AccountId);
        var second = await sut.GetPostingDates(AccountId);

        Assert.Equal([_date], second);
        inner.Verify(r => r.GetPostingDates(AccountId), Times.Once);
    }

    [Fact]
    public async Task GetYoungest_WhenOwnerUnresolved_BypassesCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetYoungest(AccountId)).ReturnsAsync(Entry());
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, null), Mock.Of<ICacheInvalidator>(), CreateCache());

        await sut.GetYoungest(AccountId);
        await sut.GetYoungest(AccountId);

        inner.Verify(r => r.GetYoungest(AccountId), Times.Exactly(2));
    }

    // -------------------------------------------------------------------------
    // Every write path invalidates the owning user's cache
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Add_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<bool>())).ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), invalidator.Object, CreateCache());

        await sut.Add(Entry());

        invalidator.Verify(i => i.InvalidateUser(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddBatch_InvalidatesOwnersCache_OncePerUser()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Add(It.IsAny<IEnumerable<CurrencyAccountEntry>>(), It.IsAny<bool>())).ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), invalidator.Object, CreateCache());

        await sut.Add([Entry(1), Entry(2), Entry(3)]);

        invalidator.Verify(i => i.InvalidateUser(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Update(It.IsAny<CurrencyAccountEntry>())).ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), invalidator.Object, CreateCache());

        await sut.Update(Entry());

        invalidator.Verify(i => i.InvalidateUser(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEntry_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Delete(AccountId, 1)).ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), invalidator.Object, CreateCache());

        await sut.Delete(AccountId, 1);

        invalidator.Verify(i => i.InvalidateUser(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccount_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Delete(AccountId)).ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), invalidator.Object, CreateCache());

        await sut.Delete(AccountId);

        invalidator.Verify(i => i.InvalidateUser(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddLabel_ResolvesOwnerFromEntry_AndInvalidates()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.AddLabel(11, 3)).ReturnsAsync(true);
        inner.Setup(r => r.GetByIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entry(11)]);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), invalidator.Object, CreateCache());

        await sut.AddLabel(11, 3);

        invalidator.Verify(i => i.InvalidateUser(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddLabels_ResolvesOwnersFromEntries_AndInvalidates()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.AddLabels(It.IsAny<IEnumerable<(int, int)>>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        inner.Setup(r => r.GetByIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entry(11), Entry(12)]);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), invalidator.Object, CreateCache());

        await sut.AddLabels([(11, 3), (12, 3)], TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // End-to-end: a write busts the cached point reads (real cache + invalidator)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_BustsCachedPointReads_NextReadHitsInnerAgain()
    {
        var cache = CreateCache();
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.SetupSequence(r => r.GetYoungest(AccountId))
            .ReturnsAsync(Entry(1, 100m))
            .ReturnsAsync(Entry(1, 175m));
        inner.Setup(r => r.Update(It.IsAny<CurrencyAccountEntry>())).ReturnsAsync(true);
        var sut = CreateSut(inner.Object, ResolverFor(AccountId, UserId), new CacheInvalidator(cache), cache);

        var before = await sut.GetYoungest(AccountId);   // miss → 100, cached
        await sut.GetYoungest(AccountId);                // hit
        await sut.Update(Entry(1, 175m));                // busts acc:u{UserId}
        var after = await sut.GetYoungest(AccountId);    // miss again → 175

        Assert.Equal(100m, before!.Value);
        Assert.Equal(175m, after!.Value);
        inner.Verify(r => r.GetYoungest(AccountId), Times.Exactly(2));
    }
}