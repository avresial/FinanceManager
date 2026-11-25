using FinanceManager.Application.Transactions.Bonds.Details;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class BondUnrealizedGainLossCalculatorTests
{
    private static readonly DateTime _asOf = new(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ICurrencyExchangeService> _exchange = new();
    private readonly BondUnrealizedGainLossCalculator _calculator;

    public BondUnrealizedGainLossCalculatorTests() => _calculator = new(_exchange.Object);

    [Fact]
    public async Task CalculateAsync_PricesThePooledPositionFromItsLatestEntry_WithoutAnAccount()
    {
        // Annual 3.65% (0.01 per day on a unit of 100): 4 days from the anchor of the latest pooled entry.
        var details = CreateDetails(DefaultCurrency.PLN, rate: 0.0365m, unitValue: 100m);
        var position = new OwnerLedgerPosition(5, 10m, new DateTime(2024, 1, 6, 8, 0, 0, DateTimeKind.Utc));

        var result = await _calculator.CalculateAsync(position, details, DefaultCurrency.PLN, _asOf);

        Assert.NotNull(result);
        Assert.Equal("Bonds", result.AccountName);
        Assert.Equal("5", result.InstrumentId);
        Assert.Equal(10m, result.Quantity);
        Assert.Equal(1000m, result.CostBasis);
        Assert.Equal(1000.4m, result.CurrentValue);
        Assert.Equal(0.4m, result.UnrealizedGainLoss);
        Assert.False(result.IsExcludedFromTotals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task CalculateAsync_ReturnsNull_WhenNothingIsHeld(int units)
    {
        var position = new OwnerLedgerPosition(5, units, _asOf.AddDays(-3));

        Assert.Null(await _calculator.CalculateAsync(position, CreateDetails(DefaultCurrency.PLN), DefaultCurrency.PLN, _asOf));
    }

    [Fact]
    public async Task CalculateAsync_ExcludesThePosition_WhenTheDefinitionIsMissing()
    {
        var result = await _calculator.CalculateAsync(new OwnerLedgerPosition(5, 3m, _asOf.AddDays(-3)), null, DefaultCurrency.PLN, _asOf);

        Assert.NotNull(result);
        Assert.True(result.IsExcludedFromTotals);
        Assert.Equal(3m, result.Quantity);
        Assert.Equal("Bonds", result.AccountName);
        Assert.Contains("Missing bond details", result.WarningMessage);
    }

    [Fact]
    public async Task CalculateAsync_ExcludesThePosition_WhenTheFxRateIsMissing()
    {
        _exchange.Setup(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, It.IsAny<DateTime>())).ReturnsAsync((decimal?)null);

        var result = await _calculator.CalculateAsync(
            new OwnerLedgerPosition(5, 3m, _asOf.AddDays(-3)), CreateDetails(DefaultCurrency.USD), DefaultCurrency.PLN, _asOf);

        Assert.NotNull(result);
        Assert.True(result.IsExcludedFromTotals);
        Assert.Contains("Missing FX rate", result.WarningMessage);
    }

    [Fact]
    public async Task CalculateAsync_ConvertsCostAndValueAtTheAsOfRate()
    {
        _exchange.Setup(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, _asOf)).ReturnsAsync(4m);

        var result = await _calculator.CalculateAsync(
            new OwnerLedgerPosition(5, 3m, _asOf.AddDays(-3)), CreateDetails(DefaultCurrency.USD, unitValue: 100m), DefaultCurrency.PLN, _asOf);

        Assert.NotNull(result);
        Assert.False(result.IsExcludedFromTotals);
        Assert.Equal(1200m, result.CostBasis);
        Assert.Equal(1200m, result.CurrentValue);
    }

    private static BondDetails CreateDetails(Currency currency, decimal rate = 0m, decimal unitValue = 1m) => new(
        "Bond",
        "Issuer",
        new DateOnly(2023, 1, 1),
        new DateOnly(2030, 1, 1),
        [new BondCalculationMethod { Id = 1, DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = rate }],
        currency,
        BondType.InflationBond,
        unitValue)
    { Id = 5 };
}