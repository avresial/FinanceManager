using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;
using Microsoft.Extensions.Caching.Hybrid;
using Moq;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Bond.Repositories;

[Trait("Category", "Unit")]
public class CachedBondEntryRepositoryTests
{
    private const int _accountId = 7;
    private static readonly DateTime _firstDate = new DateTime(2025, 1, 15, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234);
    private static readonly DateTime _adjacentDate = _firstDate.AddTicks(1);

    [Fact]
    public async Task GetNextOlder_UsesExactDatesAndCallsInnerForEveryRequest_WithoutCaching()
    {
        var first = new Dictionary<int, BondAccountEntry> { [101] = Entry(1, 101) };
        var adjacent = new Dictionary<int, BondAccountEntry> { [202] = Entry(2, 202) };
        var inner = new Mock<IBondAccountEntryRepository<BondAccountEntry>>(MockBehavior.Strict);
        inner.Setup(r => r.GetNextOlder(_accountId, _firstDate)).ReturnsAsync(first);
        inner.Setup(r => r.GetNextOlder(_accountId, _adjacentDate)).ReturnsAsync(adjacent);
        var resolver = new Mock<IAccountUserResolver>(MockBehavior.Strict);
        var invalidator = new Mock<ICacheInvalidator>(MockBehavior.Strict);
        var cache = new Mock<HybridCache>(MockBehavior.Strict);
        var sut = CreateSut(inner.Object, resolver.Object, invalidator.Object, cache.Object);

        var firstRequest = await sut.GetNextOlder(_accountId, _firstDate);
        var repeatedRequest = await sut.GetNextOlder(_accountId, _firstDate);
        var adjacentRequest = await sut.GetNextOlder(_accountId, _adjacentDate);

        Assert.Same(first, firstRequest);
        Assert.Same(first, repeatedRequest);
        Assert.Same(adjacent, adjacentRequest);
        Assert.Equal(101, Assert.Single(firstRequest).Key);
        Assert.Equal(202, Assert.Single(adjacentRequest).Key);
        inner.Verify(r => r.GetNextOlder(_accountId, _firstDate), Times.Exactly(2));
        inner.Verify(r => r.GetNextOlder(_accountId, _adjacentDate), Times.Once);
        resolver.VerifyNoOtherCalls();
        invalidator.VerifyNoOtherCalls();
        cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNextYounger_UsesExactDatesAndCallsInnerForEveryRequest_WithoutCaching()
    {
        var first = new Dictionary<int, BondAccountEntry> { [101] = Entry(1, 101) };
        var adjacent = new Dictionary<int, BondAccountEntry> { [202] = Entry(2, 202) };
        var inner = new Mock<IBondAccountEntryRepository<BondAccountEntry>>(MockBehavior.Strict);
        inner.Setup(r => r.GetNextYounger(_accountId, _firstDate)).ReturnsAsync(first);
        inner.Setup(r => r.GetNextYounger(_accountId, _adjacentDate)).ReturnsAsync(adjacent);
        var resolver = new Mock<IAccountUserResolver>(MockBehavior.Strict);
        var invalidator = new Mock<ICacheInvalidator>(MockBehavior.Strict);
        var cache = new Mock<HybridCache>(MockBehavior.Strict);
        var sut = CreateSut(inner.Object, resolver.Object, invalidator.Object, cache.Object);

        var firstRequest = await sut.GetNextYounger(_accountId, _firstDate);
        var repeatedRequest = await sut.GetNextYounger(_accountId, _firstDate);
        var adjacentRequest = await sut.GetNextYounger(_accountId, _adjacentDate);

        Assert.Same(first, firstRequest);
        Assert.Same(first, repeatedRequest);
        Assert.Same(adjacent, adjacentRequest);
        Assert.Equal(101, Assert.Single(firstRequest).Key);
        Assert.Equal(202, Assert.Single(adjacentRequest).Key);
        inner.Verify(r => r.GetNextYounger(_accountId, _firstDate), Times.Exactly(2));
        inner.Verify(r => r.GetNextYounger(_accountId, _adjacentDate), Times.Once);
        resolver.VerifyNoOtherCalls();
        invalidator.VerifyNoOtherCalls();
        cache.VerifyNoOtherCalls();
    }

    private static IBondAccountEntryRepository<BondAccountEntry> CreateSut(
        IBondAccountEntryRepository<BondAccountEntry> inner,
        IAccountUserResolver resolver,
        ICacheInvalidator invalidator,
        HybridCache cache) => new CachedBondEntryRepository(inner, resolver, invalidator, cache, new EntryRangeCacheOptions());

    private static BondAccountEntry Entry(int entryId, int bondDetailsId) =>
        new(_accountId, entryId, _firstDate, 100m, 10m, bondDetailsId);
}