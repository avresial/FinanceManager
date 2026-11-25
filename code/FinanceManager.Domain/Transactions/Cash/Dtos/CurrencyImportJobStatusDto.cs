using FinanceManager.Domain.Transactions.Import;

namespace FinanceManager.Domain.Transactions.Cash.Dtos;

public record CurrencyImportJobStatusDto(
    Guid JobId,
    AsyncImportJobState Status,
    int TotalEntries,
    int ProcessedEntries,
    int Imported,
    int Failed,
    int ConflictCount,
    int ResolvedConflictCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ImportJobConflict> Conflicts,
    bool IsCompleted);