using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class InvestmentAccountChartLoaderTests
{
    [Fact]
    public void BuildHoldings_UsesLatestTradeOnOrBeforeAsOf_SkipsZeroAndUnknown_OrdersByValue()
    {
        InvestmentTransactionDto[] metadata =
        [
            Trade(1, listingId: 10, new DateOnly(2024, 1, 1), unitPrice: 5m, ticker: "OLD"),
            Trade(2, listingId: 10, new DateOnly(2024, 1, 10), unitPrice: 6m, ticker: "AAA"),
            Trade(3, listingId: 10, new DateOnly(2024, 2, 1), unitPrice: 99m, ticker: "FUTURE"),
            Trade(4, listingId: 20, new DateOnly(2024, 1, 5), unitPrice: 100m, ticker: "BBB"),
            Trade(5, listingId: 30, new DateOnly(2024, 1, 5), unitPrice: 1m, ticker: "ZERO"),
        ];
        var holdings = new Dictionary<long, decimal> { [10] = 2m, [20] = 1m, [30] = 0m, [40] = 3m };

        var rows = InvestmentAccountChartLoader.BuildHoldings(metadata, holdings, new DateTime(2024, 1, 31));

        Assert.Collection(rows,
            row => { Assert.Equal("BBB", row.Ticker); Assert.Equal(100m, row.Value); },
            row => { Assert.Equal("AAA", row.Ticker); Assert.Equal(12m, row.Value); });
    }

    private static InvestmentTransactionDto Trade(long id, long listingId, DateOnly date, decimal unitPrice, string ticker) =>
        new(id, 1, 1, listingId, InvestmentTransactionType.Buy, 1m, unitPrice, "USD", date, null, null, ticker);
}