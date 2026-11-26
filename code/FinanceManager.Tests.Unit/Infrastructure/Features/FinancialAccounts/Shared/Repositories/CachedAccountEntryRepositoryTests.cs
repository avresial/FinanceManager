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

    private static CurrencyAccountEntry Entry(long entryId = 1, decimal value = 100m) =>
        new(null, entryId, _date, 0, value) { UserId = _userId };

    // -------------------------------------------------------------------------
    // Every write path invalidates the owning user's cache
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Add_InvalidatesOwnersCache()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Add(
                It.IsAny<CurrencyAccountEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = new CachedAccountEntryRepository<CurrencyAccountEntry>(inner.Object, invalidator.Object);

        await sut.Add(Entry(), cancellationToken: TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddBatch_InvalidatesOwnersCache_OncePerUser()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.Add(
                It.IsAny<IEnumerable<CurrencyAccountEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = new CachedAccountEntryRepository<CurrencyAccountEntry>(inner.Object, invalidator.Object);

        await sut.Add([Entry(1), Entry(2), Entry(3)], cancellationToken: TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddLabels_InvalidatesTheOwnerDirectly_ForLinkedAndUnlinkedEntries()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.AddLabels(_userId, It.IsAny<IEnumerable<(long, int)>>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var invalidator = new Mock<ICacheInvalidator>();
        // The resolver knows no account at all: an unlinked entry must still be invalidated through its owner.
        var sut = new CachedAccountEntryRepository<CurrencyAccountEntry>(inner.Object, invalidator.Object);

        var added = await sut.AddLabels(_userId, [(11, 3), (12, 3)], TestContext.Current.CancellationToken);

        Assert.Equal(2, added);
        invalidator.Verify(i => i.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(r => r.GetByIds(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddLabels_WithoutNewAssignments_LeavesTheCacheAlone()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.AddLabels(_userId, It.IsAny<IEnumerable<(long, int)>>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var invalidator = new Mock<ICacheInvalidator>();
        var sut = new CachedAccountEntryRepository<CurrencyAccountEntry>(inner.Object, invalidator.Object);

        await sut.AddLabels(_userId, [(11, 3)], TestContext.Current.CancellationToken);

        invalidator.Verify(i => i.InvalidateUser(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIds_PassesTheOwnerStraightThrough()
    {
        var inner = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        inner.Setup(r => r.GetByIds(_userId, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync([Entry(11)]);
        var sut = new CachedAccountEntryRepository<CurrencyAccountEntry>(inner.Object, Mock.Of<ICacheInvalidator>());

        var entries = await sut.GetByIds(_userId, [11], TestContext.Current.CancellationToken);

        Assert.Equal(11, Assert.Single(entries).EntryId);
        inner.Verify(r => r.GetByIds(_userId, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}