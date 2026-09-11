using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Application.Alerts.Models;

public class AlertEvaluationSnapshot
{
    public IReadOnlyList<CurrencyAccount> Accounts { get; init; } = [];
    public IReadOnlyList<CurrencyAccountEntry> RecentEntries { get; init; } = [];
    public IReadOnlyList<RecurringTransactionResult> Subscriptions { get; init; } = [];
    public DateTime EvaluationDate { get; init; } = DateTime.UtcNow;

    public AlertEvaluationSnapshot()
    {
    }

    public AlertEvaluationSnapshot(
        IReadOnlyList<CurrencyAccount> accounts,
        IReadOnlyList<RecurringTransactionResult> subscriptions,
        DateTime? evaluationDate = null)
    {
        Accounts = accounts;
        Subscriptions = subscriptions;
        EvaluationDate = evaluationDate ?? DateTime.UtcNow;
    }

    public AlertEvaluationSnapshot(
        IReadOnlyList<CurrencyAccount> accounts,
        IReadOnlyList<CurrencyAccountEntry> recentEntries,
        IReadOnlyList<RecurringTransactionResult> subscriptions,
        DateTime? evaluationDate = null)
    {
        Accounts = accounts;
        RecentEntries = recentEntries;
        Subscriptions = subscriptions;
        EvaluationDate = evaluationDate ?? DateTime.UtcNow;
    }

    public IReadOnlyList<CurrencyAccountEntry> GetAllEntries()
    {
        var entriesByAccount = Accounts
            .Where(a => a.Entries is not null)
            .SelectMany(a => a.Entries);

        return entriesByAccount
            .Concat(RecentEntries)
            .DistinctBy(e => (e.AccountId, e.EntryId))
            .ToList();
    }
}