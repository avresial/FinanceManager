using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;

public interface ICurrencyImportJobStore
{
    /// <summary>Creates a job that imports into the owner's pool of one currency, with no account.</summary>
    Guid CreateOwnerJob(int userId, int currencyId, int totalEntries);

    /// <summary>The target fixed when the job was created. False when the job does not exist or belongs to someone else.</summary>
    bool TryGetCurrencyId(Guid jobId, int userId, out int currencyId);

    /// <summary>Marks a conflict unresolved again after its decision could not be saved, so it can be retried.</summary>
    bool TryReopenConflict(Guid jobId, int userId, string conflictId);
    bool TrySetRunning(Guid jobId);
    bool TryUpdateProgress(Guid jobId, int processedEntries, int imported, int failed);
    ImportJobConflict? TryAddConflict(Guid jobId, ImportConflict conflict);
    bool TryMarkCompleted(Guid jobId, ImportResult result);
    bool TryMarkFailed(Guid jobId, string error);
    bool TryGetStatus(Guid jobId, int userId, out CurrencyImportJobStatusDto? status);
    bool CanAccessJob(Guid jobId, int userId);
    bool TryResolveConflict(Guid jobId, int userId, CurrencyImportConflictResolutionDecisionDto decision, out CurrencyEntryImport? addition);
}