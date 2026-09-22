using FinanceManager.Application.FinancialAccounts.Investments.Performance;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class PortfolioPeriodLedgerTests
{
    private static readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddMonths(1);

    [Fact]
    public void Build_SplitsTradePrincipalAndFeeAndNormalizesQuoteUnits()
    {
        var buy = new IInvestmentTransactionRepository.CapitalFlowInput(
            11, 2, 3, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
            2m, 100m, 5m, "GBX", 0.01m);
        var sell = buy with
        {
            TransactionId = 12,
            Type = InvestmentTransactionType.Sell,
            Fee = -2m,
            TradeDate = DateOnly.FromDateTime(_end.AddDays(1)),
        };

        var movements = PortfolioPeriodLedger.Build([buy, sell], [], new Dictionary<int, BondDetails>(), _start, _end);

        Assert.Collection(movements,
            principal =>
            {
                Assert.Equal(PortfolioMovementKind.Purchase, principal.Kind);
                Assert.Equal(2m, principal.Amount);
                Assert.Equal("GBP", principal.Currency);
                Assert.Equal(2m, principal.Quantity);
                Assert.Equal(11, principal.SourceId);
                Assert.Equal(2, principal.AccountId);
                Assert.Equal(3, principal.InstrumentId);
            },
            fee =>
            {
                Assert.Equal(PortfolioMovementKind.Fee, fee.Kind);
                Assert.Equal(5m, fee.Amount);
                Assert.Equal("GBP", fee.Currency);
                Assert.Equal(11, fee.SourceId);
                Assert.Null(fee.Quantity);
            });
    }

    [Fact]
    public void Build_RepresentsSalesAndFeeRebatesAsPositiveAmounts()
    {
        var sell = new IInvestmentTransactionRepository.CapitalFlowInput(
            12, 2, 3, DateOnly.FromDateTime(_start), InvestmentTransactionType.Sell,
            2m, 100m, -3m, "PLN", null);

        var movements = PortfolioPeriodLedger.Build([sell], [], new Dictionary<int, BondDetails>(), _start, _end);

        Assert.Equal(PortfolioMovementKind.Sale, movements[0].Kind);
        Assert.Equal(200m, movements[0].Amount);
        Assert.Equal(PortfolioMovementKind.FeeRebate, movements[1].Kind);
        Assert.Equal(3m, movements[1].Amount);
        Assert.Equal(movements[0].SourceId, movements[1].SourceId);
    }

    [Fact]
    public void Build_TypesBondContributionsAndWithdrawals()
    {
        var account = new BondAccount(1, 2, "Bonds",
            [new BondAccountEntry(2, 11, _start, 2m, 2m, 5),
                new BondAccountEntry(2, 12, _end, 1m, -1m, 5)]);
        var details = new BondDetails("Bond", "Issuer", DateOnly.FromDateTime(_start),
            DateOnly.FromDateTime(_end.AddYears(1)), [], DefaultCurrency.PLN,
            BondType.InflationBond, 100m)
        { Id = 5 };

        var movements = PortfolioPeriodLedger.Build([], [account], new Dictionary<int, BondDetails> { [5] = details }, _start, _end);

        Assert.Equal(PortfolioMovementKind.BondContribution, movements[0].Kind);
        Assert.Equal(200m, movements[0].Amount);
        Assert.Equal(PortfolioMovementKind.BondWithdrawal, movements[1].Kind);
        Assert.Equal(100m, movements[1].Amount);
        Assert.All(movements, movement => Assert.Equal("PLN", movement.Currency));
    }
}