using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using System.Globalization;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.Shared;

public static class AccountHistoryState
{
    public static bool HasTransactionHistory(CurrencyAccount? account) =>
        account?.Entries.Any() == true
        || account?.NextOlderEntry is not null
        || account?.NextYoungerEntry is not null;

    public static bool HasTransactionHistory(StockAccount? account) =>
        account?.Entries.Any() == true
        || account?.NextOlderEntries.Any() == true
        || account?.NextYoungerEntries.Any() == true;

    public static bool HasTransactionHistory(BondAccount? account) =>
        account?.Entries.Any() == true
        || account?.NextOlderEntries.Any() == true
        || account?.NextYoungerEntries.Any() == true;

    public static string EmptyRangeMessage(DateTime startDate, DateTime endDate) =>
        $"No transaction records for selected time range: {FormatDate(startDate)} - {FormatDate(endDate)}.";

    private static string FormatDate(DateTime date) =>
        date.Date.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
}