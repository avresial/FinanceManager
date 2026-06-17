namespace FinanceManager.Domain.FinancialAccounts.Shared.Imports;

public enum AsyncImportJobState
{
    Queued = 0,
    Running = 1,
    WaitingForResolution = 2,
    Completed = 3,
    CompletedWithConflicts = 4,
    Failed = 5
}