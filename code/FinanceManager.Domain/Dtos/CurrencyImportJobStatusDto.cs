using FinanceManager.Domain.Entities.Imports;

namespace FinanceManager.Domain.Dtos;

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
