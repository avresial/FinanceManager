using FinanceManager.Application.FinancialAccounts.Currencies.Balance;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyBalanceServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly CurrencyBalanceService _balanceService;

    public CurrencyBalanceServiceTests()
    {
        _balanceService = new CurrencyBalanceService(_ledger);
    }

    // Without an account filter the balances pool the owner's cash entries by currency; the tests below it keep the account-filtered path covered.
    [Fact]
    public async Task GetClosingBalance_Unfiltered_PoolsEveryCurrencyAndCarriesBalancesAcrossEmptyDays()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(0, start.AddDays(-1), 100m)
            .Add(0, start.AddDays(2).AddHours(9), 50m)
            .Add(2, start.AddDays(1).AddHours(9), 30m)
            .Add(0, start.AddDays(2).AddHours(10), -20m);

        var result = await _balanceService.GetClosingBalance(1, DefaultCurrency.PLN, start, start.AddDays(3));

        Assert.Equal([100m, 130m, 160m, 160m], result.Select(x => x.Value));
    }

    [Fact]
    public async Task GetClosingBalance_Unfiltered_StartsFromTheBalanceBeforeTheRange_AndAcceptsNegativeGuestIds()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(0, start.AddDays(-10), 70m).Add(0, start.AddDays(-3), -5m);

        var result = await _balanceService.GetClosingBalance(-3, DefaultCurrency.PLN, start, start.AddDays(1));

        Assert.Equal([65m, 65m], result.Select(x => x.Value));
    }

    [Fact]
    public async Task GetFlows_Unfiltered_AggregateTheOwnersEntriesPerDayAcrossCurrenciesAndIgnoreOutOfRangeOnes()
    {
        var start = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(0, start.AddDays(-2), 500m)
            .Add(0, start.AddDays(1), 100m)
            .Add(2, start.AddDays(1).AddHours(2), -30m)
            .Add(0, start.AddDays(10), 230m);

        var inflow = await _balanceService.GetInflow(1, DefaultCurrency.PLN, start, start.AddDays(3));
        var outflow = await _balanceService.GetOutflow(1, DefaultCurrency.PLN, start, start.AddDays(3));
        var net = await _balanceService.GetNetCashFlow(1, DefaultCurrency.PLN, start, start.AddDays(3));

        Assert.Equal([0m, 100m, 0m, 0m], inflow.Select(x => x.Value));
        Assert.Equal([0m, -30m, 0m, 0m], outflow.Select(x => x.Value));
        Assert.Equal([0m, 70m, 0m, 0m], net.Select(x => x.Value));
    }
}