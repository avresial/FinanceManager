using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Transactions.Cash.Dtos;

namespace FinanceManager.Application.Alerts.Services.Conditions;

internal static class AlertTransactionReferences
{
    private const int _maxMatchingTransactions = 5;

    public static IReadOnlyList<AlertTransactionReference> From(
        IEnumerable<CashLogEntry> entries,
        IReadOnlyDictionary<int, string> currencyCodes,
        bool includeAllMatchingTransactions)
    {
        IEnumerable<CashLogEntry> orderedEntries = entries
            .OrderByDescending(entry => Math.Abs(entry.ValueChange))
            .ThenByDescending(entry => entry.PostingDate)
            .ThenByDescending(entry => entry.EntryId);

        if (!includeAllMatchingTransactions)
            orderedEntries = orderedEntries.Take(_maxMatchingTransactions);

        return orderedEntries.Select(entry => new AlertTransactionReference(
                entry.EntryId,
                entry.PostingDate,
                Math.Abs(entry.ValueChange),
                entry.Description,
                entry.ContractorDetails,
                entry.Currency ?? currencyCodes.GetValueOrDefault(entry.CurrencyId)))
            .ToList();
    }
}