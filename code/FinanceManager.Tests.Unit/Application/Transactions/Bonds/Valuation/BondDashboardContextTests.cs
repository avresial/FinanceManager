using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Transactions.Bonds.Valuation;

[Collection("Application")]
[Trait("Category", "Unit")]
public class BondDashboardContextTests
{
    [Fact]
    public async Task LoadReferencedDetailsAsync_ById_LoadsOnlyIdsNotRequestedYet()
    {
        var requestedIds = new List<int[]>();
        var repository = new Mock<IBondDetailsRepository>();
        repository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<int> ids, CancellationToken _) => requestedIds.Add(ids.ToArray()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) => (IReadOnlyList<BondDetails>)[.. ids.Select(id => CreateDetails(id))]);
        var context = new BondDashboardContext(repository.Object);

        await context.LoadReferencedDetailsAsync(new[] { 1, 2, 2 }, TestContext.Current.CancellationToken);
        var loaded = await context.LoadReferencedDetailsAsync(new[] { 2, 3 }, TestContext.Current.CancellationToken);

        Assert.Equal([[1, 2], [3]], requestedIds);
        Assert.Equal([1, 2, 3], loaded.Keys.Order());
    }

    [Fact]
    public async Task GetOrComputePrice_Pooled_MemoizesPerBondAnchorUnitsAndDate()
    {
        var repository = new Mock<IBondDetailsRepository>();
        repository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BondDetails>)[CreateDetails(1, unitValue: 100m)]);
        var context = new BondDashboardContext(repository.Object);
        await context.LoadReferencedDetailsAsync(new[] { 1 }, TestContext.Current.CancellationToken);
        var anchor = new DateOnly(2024, 1, 1);
        var date = new DateOnly(2024, 1, 2);

        Assert.Equal(1_000m, context.GetOrComputePrice(1, anchor, 10m, date));
        Assert.Equal(1_000m, context.GetOrComputePrice(1, anchor, 10m, date, () => throw new InvalidOperationException("Should reuse the price")));
        Assert.Equal(1_200m, context.GetOrComputePrice(1, anchor, 12m, date));
        Assert.Equal(0m, context.GetOrComputePrice(1, anchor, 12m, anchor.AddDays(-1)));
        Assert.Equal(7m, context.GetOrComputePrice(1, anchor, 10m, date.AddDays(1), () => 7m));
    }

    private static BondDetails CreateDetails(int id, decimal unitValue = 100m) =>
        new(
            "Bond",
            "Issuer",
            new DateOnly(2023, 1, 1),
            new DateOnly(2025, 1, 1),
            [new BondCalculationMethod
            {
                DateOperator = DateOperator.UntilDate,
                DateValue = "2025-01-01",
                Rate = 0m,
            }],
            DefaultCurrency.PLN,
            BondType.InflationBond,
            unitValue)
        {
            Id = id,
        };
}