using FinanceManager.Application.Portfolio.Performance;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class PortfolioPeriodLedgerTests
{
    private static readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = _start.AddMonths(1);

    [Fact]
    public void Build_SplitsTradePrincipalAndFeeAndNormalizesQuoteUnits()
    {
        var buy = new IInvestmentTransactionRepository.UserCapitalFlowInput(
            11, 3, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
            2m, 100m, 5m, "GBX");
        var sell = buy with
        {
            TransactionId = 12,
            Type = InvestmentTransactionType.Sell,
            Fee = -2m,
            TradeDate = DateOnly.FromDateTime(_end.AddDays(1)),
        };

        var ledger = PortfolioPeriodLedger.Build([buy, sell], [], new Dictionary<int, BondDetails>(), _start, _end);
        var movements = ledger.Movements;

        Assert.Single(ledger.QuantityChanges);
        Assert.Equal(2m, ledger.QuantityChanges[0].Quantity);

        Assert.Collection(movements,
            principal =>
            {
                Assert.Equal(PortfolioMovementKind.Purchase, principal.Kind);
                Assert.Equal(2m, principal.Amount);
                Assert.Equal("GBP", principal.Currency);
                Assert.Equal(2m, principal.Quantity);
                Assert.Equal(11, principal.SourceId);
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

    [Theory]
    [InlineData("GBX", "GBP")]
    [InlineData("GBP.", "GBP")]
    [InlineData("ZAC", "ZAR")]
    [InlineData("ILA", "ILS")]
    public void Build_NormalizesMinorTransactionCurrencyWithoutListingMultiplier(string minorCurrency, string majorCurrency)
    {
        var buy = new IInvestmentTransactionRepository.UserCapitalFlowInput(
            11, 3, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
            2m, 100m, null, minorCurrency);

        var ledger = PortfolioPeriodLedger.Build([buy], [], new Dictionary<int, BondDetails>(), _start, _end);

        var principal = Assert.Single(ledger.Movements);
        Assert.Equal(2m, principal.Amount);
        Assert.Equal(majorCurrency, principal.Currency);
    }

    [Fact]
    public void Build_RepresentsSalesAndFeeRebatesAsPositiveAmounts()
    {
        var sell = new IInvestmentTransactionRepository.UserCapitalFlowInput(
            12, 3, DateOnly.FromDateTime(_start), InvestmentTransactionType.Sell,
            2m, 100m, -3m, "PLN");

        var movements = PortfolioPeriodLedger.Build([sell], [], new Dictionary<int, BondDetails>(), _start, _end).Movements;

        Assert.Equal(PortfolioMovementKind.Sale, movements[0].Kind);
        Assert.Equal(200m, movements[0].Amount);
        Assert.Equal(PortfolioMovementKind.FeeRebate, movements[1].Kind);
        Assert.Equal(3m, movements[1].Amount);
        Assert.Equal(movements[0].SourceId, movements[1].SourceId);
    }

    [Fact]
    public void Build_TypesBondContributionsAndWithdrawals()
    {
        List<OwnerLedgerPoint> points =
            [new(5, 11, _start, 2m),
                new(5, 12, _end, -1m)];
        var details = new BondDetails("Bond", "Issuer", DateOnly.FromDateTime(_start),
            DateOnly.FromDateTime(_end.AddYears(1)), [], DefaultCurrency.PLN,
            BondType.InflationBond, 100m)
        { Id = 5 };

        var movements = PortfolioPeriodLedger.Build([], points, new Dictionary<int, BondDetails> { [5] = details }, _start, _end).Movements;

        Assert.Equal(PortfolioMovementKind.BondContribution, movements[0].Kind);
        Assert.Equal(200m, movements[0].Amount);
        Assert.Equal(PortfolioMovementKind.BondWithdrawal, movements[1].Kind);
        Assert.Equal(100m, movements[1].Amount);
        Assert.All(movements, movement => Assert.Equal("PLN", movement.Currency));
    }

    [Fact]
    public void Build_KeepsQuantityForZeroPriceTradeWithoutZeroAmountMovement()
    {
        var buy = new IInvestmentTransactionRepository.UserCapitalFlowInput(
            13, 3, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
            2m, 0m, null, "EUR");

        var ledger = PortfolioPeriodLedger.Build([buy], [], new Dictionary<int, BondDetails>(), _start, _end);

        Assert.Empty(ledger.Movements);
        var quantityChange = Assert.Single(ledger.QuantityChanges);
        Assert.Equal(PortfolioMovementKind.Purchase, quantityChange.Kind);
        Assert.Equal(2m, quantityChange.Quantity);
        Assert.Equal(13, quantityChange.SourceId);
    }
}