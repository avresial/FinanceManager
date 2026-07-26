using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using System.Globalization;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared;

public static class AccountHistoryState
{
    public static bool HasTransactionHistory(CurrencyAccount? account) =>
        account?.Entries.Any() == true
        || account?.NextOlderEntry is not null
        || account?.NextYoungerEntry is not null;

    public static bool HasTransactionHistory(BondAccount? account) =>
        account?.Entries.Any() == true
        || account?.NextOlderEntries.Any() == true
        || account?.NextYoungerEntries.Any() == true;

    public static string EmptyRangeMessage(DateTime startDate, DateTime endDate) =>
        $"No transaction records for selected time range: {FormatDate(startDate)} - {FormatDate(endDate)}.";

    private static string FormatDate(DateTime date) =>
        date.Date.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
}