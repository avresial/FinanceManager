namespace FinanceManager.Application.FinancialAccounts.Shared;

public sealed record EntryRangeResult<TEntry>(IReadOnlyList<TEntry> Entries, DateTime EffectiveStartDate);