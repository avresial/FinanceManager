using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;

public interface ICurrencyImportJobStore
{
    Guid CreateJob(int userId, int accountId, int totalEntries);
    bool TrySetRunning(Guid jobId);
    bool TryUpdateProgress(Guid jobId, int processedEntries, int imported, int failed);
    ImportJobConflict? TryAddConflict(Guid jobId, ImportConflict conflict);
    bool TryMarkCompleted(Guid jobId, ImportResult result);
    bool TryMarkFailed(Guid jobId, string error);
    bool TryGetStatus(Guid jobId, int userId, out CurrencyImportJobStatusDto? status);
    bool CanAccessJob(Guid jobId, int userId);
    bool TryResolveConflict(Guid jobId, int userId, CurrencyImportConflictResolutionDecisionDto decision, out ResolvedImportConflict? resolvedConflict);
}