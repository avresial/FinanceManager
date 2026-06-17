using FinanceManager.Components.Components.Features.FinancialAccounts.Shared;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using System.Globalization;

namespace FinanceManager.Tests.Unit.Components.FinancialAccounts;

[Collection("Api")]
[Trait("Category", "Unit")]
public class AccountHistoryStateTests
{
    [Fact]
    public void HasTransactionHistory_ReturnsFalse_WhenCurrencyAccountHasNoEntriesOrRangeMetadata()
    {
        var account = new CurrencyAccount(1, 1, "Empty cash", AccountLabel.Cash);

        var hasHistory = AccountHistoryState.HasTransactionHistory(account);

        Assert.False(hasHistory);
    }

    [Fact]
    public void HasTransactionHistory_ReturnsFalse_WhenStockAccountHasNoEntriesOrRangeMetadata()
    {
        var account = new StockAccount(1, 1, "Empty stocks");

        var hasHistory = AccountHistoryState.HasTransactionHistory(account);

        Assert.False(hasHistory);
    }

    [Fact]
    public void HasTransactionHistory_ReturnsFalse_WhenBondAccountHasNoEntriesOrRangeMetadata()
    {
        var account = new BondAccount(1, 1, "Empty bonds", AccountLabel.Bond);

        var hasHistory = AccountHistoryState.HasTransactionHistory(account);

        Assert.False(hasHistory);
    }

    [Fact]
    public void HasTransactionHistory_ReturnsTrue_WhenCurrencyAccountHasOnlyOlderRangeMetadata()
    {
        var account = new CurrencyAccount(
            1,
            1,
            "Cash",
            [],
            AccountLabel.Cash,
            new CurrencyAccountEntry(1, 1, new DateTime(2026, 3, 1), 100m, 100m));

        var hasHistory = AccountHistoryState.HasTransactionHistory(account);

        Assert.True(hasHistory);
    }

    [Fact]
    public void HasTransactionHistory_ReturnsTrue_WhenStockAccountHasOnlyOlderRangeMetadata()
    {
        var account = new StockAccount(
            1,
            1,
            "Stocks",
            [],
            new Dictionary<string, StockAccountEntry>
            {
                ["AAPL"] = new(1, 1, new DateTime(2026, 3, 1), 10m, 10m, "AAPL", InvestmentType.Stock)
            });

        var hasHistory = AccountHistoryState.HasTransactionHistory(account);

        Assert.True(hasHistory);
    }

    [Fact]
    public void HasTransactionHistory_ReturnsTrue_WhenBondAccountHasOnlyYoungerRangeMetadata()
    {
        var account = new BondAccount(
            1,
            1,
            "Bonds",
            [],
            AccountLabel.Bond,
            nextYoungerEntries: new Dictionary<int, BondAccountEntry>
            {
                [101] = new(1, 1, new DateTime(2026, 7, 1), 100m, 100m, 101)
            });

        var hasHistory = AccountHistoryState.HasTransactionHistory(account);

        Assert.True(hasHistory);
    }

    [Fact]
    public void EmptyRangeMessage_IncludesSelectedTimeRange()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var message = AccountHistoryState.EmptyRangeMessage(
                new DateTime(2026, 6, 1),
                new DateTime(2026, 6, 9, 15, 30, 0));

            Assert.Equal("No transaction records for selected time range: June 1, 2026 - June 9, 2026.", message);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}