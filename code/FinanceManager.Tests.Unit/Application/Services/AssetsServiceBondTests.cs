using FinanceManager.Application.FinancialAccounts.Bond.Assets;
using FinanceManager.Application.FinancialAccounts.Bond.Details;
using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class AssetsServiceBondTests
{
    private static readonly DateTime _day = new(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly AssetsServiceBond _service;

    public AssetsServiceBondTests()
    {
        _bondDetailsRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) => ids.Where(id => id != 99).Select(CreateDetails).ToList());
        _service = new AssetsServiceBond(
            _ledger,
            new BondDashboardContext(_bondDetailsRepositoryMock.Object),
            Mock.Of<IFinancialAccountRepository>(),
            _bondDetailsRepositoryMock.Object,
            new BondUnrealizedGainLossCalculator(Mock.Of<ICurrencyExchangeService>()));
    }

    [Fact]
    public async Task GetEndAssetsDistribution_PoolsUnitsPerBondAcrossEntries_AndNamesTheBondNotAnAccount()
    {
        _ledger.Add(1, _day.AddDays(-5), 10m).Add(2, _day.AddHours(8), 5m).Add(1, _day.AddHours(9), 2m);

        var distribution = await _service.GetEndAssetsDistribution(1, DefaultCurrency.PLN, _day.AddHours(12), TestContext.Current.CancellationToken);

        Assert.Equal([("Bond 1", 12m), ("Bond 2", 5m)], distribution.PerAccount.Select(x => (x.Name, x.Value)));
        Assert.All(distribution.PerAccount, x => Assert.Null(x.AccountId));
        var total = Assert.Single(distribution.PerType);
        Assert.Equal(("Bond", 17m), (total.Name, total.Value));
    }

    [Fact]
    public async Task GetEndAssetsDistribution_OmitsBondsSoldToZero_AndKeepsTheOthers()
    {
        _ledger.Add(1, _day.AddDays(-5), 10m).Add(1, _day.AddHours(9), -10m).Add(2, _day.AddHours(8), 5m);

        var distribution = await _service.GetEndAssetsDistribution(1, DefaultCurrency.PLN, _day.AddHours(12), TestContext.Current.CancellationToken);

        Assert.Equal("Bond 2", Assert.Single(distribution.PerAccount).Name);
        Assert.Equal(5m, Assert.Single(distribution.PerType).Value);
    }

    [Fact]
    public async Task GetEndAssetsDistribution_IsEmpty_WhenEveryBondIsSold()
    {
        _ledger.Add(1, _day.AddDays(-5), 10m).Add(1, _day.AddHours(9), -10m);

        var distribution = await _service.GetEndAssetsDistribution(1, DefaultCurrency.PLN, _day.AddHours(12), TestContext.Current.CancellationToken);

        Assert.Empty(distribution.PerType);
        Assert.Empty(distribution.PerAccount);
        Assert.False(await _service.IsAnyAccountWithAssets(1));
    }

    [Fact]
    public async Task GetEndAssetsPerAccountAndPerType_MatchTheDistribution()
    {
        _ledger.Add(1, _day.AddDays(-5), 10m);

        var perAccount = await _service.GetEndAssetsPerAccount(1, DefaultCurrency.PLN, _day).ToListAsync(TestContext.Current.CancellationToken);
        var perType = await _service.GetEndAssetsPerType(1, DefaultCurrency.PLN, _day).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(("Bond 1", 10m), (perAccount[0].Name, perAccount[0].Value));
        Assert.Equal(("Bond", 10m), (perType[0].Name, perType[0].Value));
    }

    [Fact]
    public async Task GetEndAssetsDistribution_AcceptsNegativeGuestIds_AndKeepsTheMissingDefinitionError()
    {
        _ledger.Add(1, _day.AddDays(-5), 10m);
        Assert.NotEmpty((await _service.GetEndAssetsDistribution(-4, DefaultCurrency.PLN, _day, TestContext.Current.CancellationToken)).PerAccount);

        _ledger.Add(99, _day.AddDays(-5), 1m);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetEndAssetsDistribution(-4, DefaultCurrency.PLN, _day, TestContext.Current.CancellationToken));
        Assert.Equal("Bond valuation requires details for bond id 99.", error.Message);
    }

    [Fact]
    public async Task IsAnyAccountWithAssets_IsTrue_ForAHeldBond()
    {
        _ledger.Add(1, DateTime.UtcNow.AddDays(-1), 3m);

        Assert.True(await _service.IsAnyAccountWithAssets(1));
    }

    [Fact]
    public async Task GetAssetsTimeSeries_CarriesPooledHoldingsForwardEveryDay()
    {
        _ledger.Add(1, _day.AddDays(-5), 10m).Add(2, _day.AddDays(1).AddHours(5), 5m);

        var result = await _service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, _day, _day.AddDays(2));

        Assert.Equal([10m, 15m, 15m], result.Select(x => x.Value));
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ShowsZeroOnceTheBondIsSold()
    {
        _ledger.Add(1, _day.AddDays(-5), 10m).Add(1, _day.AddDays(1).AddHours(5), -10m);

        var result = await _service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, _day, _day.AddDays(2));

        Assert.Equal([10m, 0m, 0m], result.Select(x => x.Value));
    }

    [Fact]
    public async Task GetAssetsTimeSeries_IsEmpty_WithoutAnyBondEntries_AndFiltersByInvestmentType()
    {
        Assert.Empty(await _service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, _day, _day.AddDays(2)));

        _ledger.Add(1, _day, 1m);
        Assert.NotEmpty(await _service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, _day, _day.AddDays(2), InvestmentType.Bond));
        Assert.Empty(await _service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, _day, _day.AddDays(2), InvestmentType.Stock));
    }

    private static BondDetails CreateDetails(int id) => new(
        $"Bond {id}",
        "Issuer",
        new DateOnly(2023, 1, 1),
        new DateOnly(2030, 1, 1),
        [new BondCalculationMethod { Id = id, DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = 0m }],
        DefaultCurrency.PLN,
        BondType.InflationBond,
        1m)
    { Id = id };
}