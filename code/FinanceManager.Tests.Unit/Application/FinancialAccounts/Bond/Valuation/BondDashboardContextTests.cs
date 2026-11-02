using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.FinancialAccounts.Bond.Valuation;

[Collection("Application")]
[Trait("Category", "Unit")]
public class BondDashboardContextTests
{
    [Fact]
    public async Task LoadReferencedDetailsAsync_BatchesDistinctIds_AndReusesTheRequestSnapshot()
    {
        var details = new[]
        {
            CreateDetails(1),
            CreateDetails(2),
        };
        var requestedIds = new List<int[]>();
        var repository = new Mock<IBondDetailsRepository>();
        repository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<int> ids, CancellationToken _) => requestedIds.Add(ids.ToArray()))
            .ReturnsAsync((IReadOnlyList<BondDetails>)details);

        var context = new BondDashboardContext(repository.Object);
        var account = new BondAccount(
            1,
            10,
            "Bonds",
            [
                new BondAccountEntry(10, 1, new DateTime(2024, 1, 1), 10m, 10m, 1),
                new BondAccountEntry(10, 2, new DateTime(2024, 1, 2), 20m, 10m, 1),
            ],
            AccountLabel.Other,
            new Dictionary<int, BondAccountEntry>
            {
                [2] = new BondAccountEntry(10, 3, new DateTime(2023, 12, 1), 5m, 5m, 2),
            });

        var first = await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);
        var second = await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);

        Assert.Single(requestedIds);
        Assert.Equal(new[] { 1, 2 }, requestedIds[0].OrderBy(x => x));
        Assert.Same(details[0], first[1]);
        Assert.Same(details[0], second[1]);
        Assert.Same(details[1], second[2]);
    }

    [Fact]
    public async Task LoadReferencedDetailsAsync_RemembersMissingIds_AndPreservesMissingDefinitionError()
    {
        var requestedIds = new List<int[]>();
        var repository = new Mock<IBondDetailsRepository>();
        repository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<int> ids, CancellationToken _) => requestedIds.Add(ids.ToArray()))
            .ReturnsAsync((IReadOnlyList<BondDetails>)[]);

        var context = new BondDashboardContext(repository.Object);
        var account = new BondAccount(
            1,
            10,
            "Bonds",
            [new BondAccountEntry(10, 1, new DateTime(2024, 1, 1), 10m, 10m, 99)],
            AccountLabel.Other);

        await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);
        await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);

        Assert.Single(requestedIds);
        var exception = Assert.Throws<InvalidOperationException>(() => context.GetDetails(99));
        Assert.Equal("Bond valuation requires details for bond id 99.", exception.Message);
    }

    [Fact]
    public async Task GetOrComputePrice_ReusesMatchingPriceWithinTheRequest()
    {
        var details = CreateDetails(1, unitValue: 100m);
        var repository = new Mock<IBondDetailsRepository>();
        repository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BondDetails>)[details]);

        var context = new BondDashboardContext(repository.Object);
        var account = new BondAccount(
            1,
            10,
            "Bonds",
            [new BondAccountEntry(10, 1, new DateTime(2024, 1, 1), 10m, 10m, 1)],
            AccountLabel.Other);
        await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);

        var entry = account.Entries[0];
        var first = context.GetOrComputePrice(account.AccountId, entry, new DateOnly(2024, 1, 2));
        var second = context.GetOrComputePrice(account.AccountId, entry, new DateOnly(2024, 1, 2));

        Assert.Equal(1_000m, first);
        Assert.Equal(first, second);

        entry.Value = 12m;
        Assert.Equal(1_200m, context.GetOrComputePrice(account.AccountId, entry, new DateOnly(2024, 1, 2)));
    }

    [Fact]
    public async Task GetOrComputePrice_UsesFreshDefinitionsAfterAnEditInANewRequest()
    {
        var originalDetails = CreateDetails(1, unitValue: 100m);
        var updatedDetails = CreateDetails(1, unitValue: 125m);
        var entry = new BondAccountEntry(10, 1, new DateTime(2024, 1, 1), 10m, 10m, 1);
        var account = new BondAccount(1, 10, "Bonds", [entry], AccountLabel.Other);

        var originalRepository = new Mock<IBondDetailsRepository>();
        originalRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BondDetails>)[originalDetails]);
        var originalContext = new BondDashboardContext(originalRepository.Object);
        await originalContext.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);

        var updatedRepository = new Mock<IBondDetailsRepository>();
        updatedRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BondDetails>)[updatedDetails]);
        var updatedContext = new BondDashboardContext(updatedRepository.Object);
        await updatedContext.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);

        var date = new DateOnly(2024, 1, 2);
        Assert.Equal(1_000m, originalContext.GetOrComputePrice(10, entry, date));
        Assert.Equal(1_250m, updatedContext.GetOrComputePrice(10, entry, date));
    }

    [Fact]
    public async Task GetOrComputePrice_ThrowsForMissingDefinition()
    {
        var repository = new Mock<IBondDetailsRepository>();
        repository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BondDetails>)[]);
        var context = new BondDashboardContext(repository.Object);
        var account = new BondAccount(
            1,
            10,
            "Bonds",
            [new BondAccountEntry(10, 1, new DateTime(2024, 1, 1), 10m, 10m, 42)],
            AccountLabel.Other);
        await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.GetOrComputePrice(10, account.Entries[0], new DateOnly(2024, 1, 2)));

        Assert.Equal("Bond valuation requires details for bond id 42.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GetOrComputePrice_LazyHistory_MatchesPointPricingInEveryDateOrder(int order)
    {
        var details = CreateDetails(1, unitValue: 1.23456m);
        details.CalculationMethods =
        [
            new() { BondDetails = details, DateValue = "2023-12-31", Rate = 0.037m },
            new() { BondDetails = null, DateValue = "2025-01-01", Rate = 1m },
            new() { BondDetails = details, DateValue = "2024-01-05", Rate = 0.061m }
        ];
        var entry = new BondAccountEntry(10, 1, new DateTime(2023, 1, 1, 18, 0, 0), 7.12345m, 0m, 1);
        var account = new BondAccount(1, 10, "Bonds", [entry], AccountLabel.Other);
        var repository = new Mock<IBondDetailsRepository>();
        repository.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([details]);
        var context = new BondDashboardContext(repository.Object);
        await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);
        var posting = DateOnly.FromDateTime(entry.PostingDate);
        int[] offsets = [-1, 0, 1, 364, 365, 366, 369, 370, 730];
        if (order == 1) Array.Reverse(offsets);
        if (order == 2) offsets = [370, 0, 365, -1, 730, 1, 364, 369, 366];
        var through = posting.AddDays(369);
        var history = entry.GetPrice(through, details);
        foreach (var offset in offsets)
        {
            var date = posting.AddDays(offset);
            var expected = entry.GetPriceAt(date, details);
            Assert.Equal(expected, context.GetOrComputePrice(10, entry, date,
                () => date <= through ? history.GetValueOrDefault(date) : entry.GetPriceAt(date, details)));
            // Exact-date sharing still wins before the lazy calculation, in either call order.
            Assert.Equal(expected, context.GetOrComputePrice(10, entry, date,
                () => throw new InvalidOperationException("Should reuse the exact-date price")));
            Assert.Equal(expected, context.GetOrComputePrice(10, entry, date));
        }
        var pointFirst = posting.AddDays(2);
        var pointPrice = context.GetOrComputePrice(10, entry, pointFirst);
        Assert.Equal(pointPrice, context.GetOrComputePrice(10, entry, pointFirst,
            () => throw new InvalidOperationException("Should reuse point pricing")));
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