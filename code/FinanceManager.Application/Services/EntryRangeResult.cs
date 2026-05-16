namespace FinanceManager.Application.Services;

public sealed record EntryRangeResult<TEntry>(IReadOnlyList<TEntry> Entries, DateTime EffectiveStartDate);