using FinanceManager.Application.FinancialAccounts.Investments.Performance;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Tests.Unit.Application.FinancialAccounts.Investments.Performance;

[Trait("Category", "Unit")]
public class PortfolioRatesTests
{
    private static readonly Currency _pln = new(1, "PLN", "zł");
    private static readonly DateTime _start = new(2026, 1, 1);
    private static readonly DateTime _end = new(2026, 1, 31);

    [Fact]
    public void TryGetRatePair_SameCurrency_IsOneToOne()
    {
        Assert.True(PortfolioRates.TryGetRatePair("pln", _pln, _start, _end, new Dictionary<string, IReadOnlyDictionary<DateTime, decimal>>(), out var startRate, out var endRate));
        Assert.Equal(1m, startRate);
        Assert.Equal(1m, endRate);
    }

    [Fact]
    public void TryGetRatePair_UsesBothEdges_AndFailsWhenAnEdgeIsMissing()
    {
        var rates = new Dictionary<string, IReadOnlyDictionary<DateTime, decimal>>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new Dictionary<DateTime, decimal> { [_start] = 4.0m, [_end] = 4.2m },
            ["EUR"] = new Dictionary<DateTime, decimal> { [_start] = 4.3m },
        };

        Assert.True(PortfolioRates.TryGetRatePair("USD", _pln, _start, _end, rates, out var startRate, out var endRate));
        Assert.Equal((4.0m, 4.2m), (startRate, endRate));
        Assert.False(PortfolioRates.TryGetRatePair("EUR", _pln, _start, _end, rates, out _, out _));
    }

    [Theory]
    [InlineData(" USD ", "USD")]
    [InlineData("GBX", "GBP")]
    public void NormalizeCurrency_TrimsAndMapsMinorUnits(string input, string expected) =>
        Assert.Equal(expected, PortfolioRates.NormalizeCurrency(input));
}