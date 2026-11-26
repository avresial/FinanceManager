using FinanceManager.Application.FinancialAccounts.Bond.Balance;
using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class BondBalanceServiceTests
{
    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeServiceMock = new();
    private readonly BondBalanceService _service;

    public BondBalanceServiceTests()
    {
        _service = new BondBalanceService(
            _ledger,
            new BondDashboardContext(_bondDetailsRepositoryMock.Object),
            _currencyExchangeServiceMock.Object);
    }

    // Without an account filter the balances pool the owner's entries per bond definition.
    [Fact]
    public async Task GetClosingBalance_Unfiltered_PoolsUnitsAcrossEntriesAndShowsZeroAfterASale()
    {
        DateTime start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(1, start.AddDays(-3), 100).Add(1, start.AddHours(8), 20).Add(1, start.AddDays(2).AddHours(8), -120);
        SetupDetails(CreateDetails(start, start.AddDays(3), DefaultCurrency.PLN));

        var result = await _service.GetClosingBalance(1, DefaultCurrency.PLN, start, start.AddDays(3));

        Assert.Equal([12000m, 12000m, 0m, 0m], result.Select(x => x.Value));
    }

    [Fact]
    public async Task GetClosingBalance_Unfiltered_IsEmpty_WithoutBondEntries()
    {
        Assert.Empty(await _service.GetClosingBalance(1, DefaultCurrency.PLN, new DateTime(2024, 1, 1), new DateTime(2024, 1, 3)));
    }

    [Fact]
    public async Task GetNetCashFlow_Unfiltered_SignsTheUnitMovementsOfEveryBond()
    {
        DateTime start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(1, start, 100).Add(1, start.AddDays(1), -50);
        SetupDetails(CreateDetails(start, start.AddDays(1), DefaultCurrency.PLN));

        var result = await _service.GetNetCashFlow(1, DefaultCurrency.PLN, start, start.AddDays(1));
        var inflow = await _service.GetInflow(1, DefaultCurrency.PLN, start, start.AddDays(1));
        var outflow = await _service.GetOutflow(-1, DefaultCurrency.PLN, start, start.AddDays(1));

        Assert.Equal([10000m, -5000m], result.Select(x => x.Value));
        Assert.Equal(10000m, Assert.Single(inflow).Value);
        Assert.Equal(-5000m, Assert.Single(outflow).Value);
    }

    [Fact]
    public async Task GetCapital_Unfiltered_CarriesOpeningUnits_CombinesSameDayEntries_AndExcludesInterest()
    {
        DateTime start = new(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(1, start.AddDays(-1), 10).Add(1, start, 2).Add(1, start.AddHours(1), 1).Add(1, start.AddDays(1), -5);
        SetupDetails(CreateDetails(start, start.AddDays(2), DefaultCurrency.PLN));

        var result = await _service.GetCapital(1, DefaultCurrency.PLN, start, start.AddDays(2));

        Assert.Equal([1_300m, 800m, 800m], result.Select(x => x.Value));
    }

    [Fact]
    public async Task GetCapital_Unfiltered_IgnoresABondBoughtAndFullySoldBeforeTheRange()
    {
        DateTime start = new(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(1, start.AddDays(-9), 10).Add(1, start.AddDays(-5), -10);
        // The closed bond's definition is gone; a bond with no units must not need it.
        SetupDetails();

        Assert.Empty(await _service.GetCapital(1, DefaultCurrency.PLN, start, start.AddDays(2)));
    }

    [Fact]
    public async Task GetClosingBalance_Unfiltered_KeepsTheMissingDefinitionError()
    {
        DateTime start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(1, start, 1);
        SetupDetails();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetClosingBalance(1, DefaultCurrency.PLN, start, start));
        Assert.Equal("Bond valuation requires details for bond id 1.", error.Message);
    }

    private void SetupDetails(params BondDetails[] details) =>
        _bondDetailsRepositoryMock.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(details);

    private static BondDetails CreateDetails(DateTime startDate, DateTime endDate, Currency currency) =>
        new(
            "Bond",
            "Issuer",
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(endDate.AddYears(1)),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0 }],
            currency,
            BondType.InflationBond,
            100m)
        {
            Id = 1
        };
}