using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Labels.Dtos;

namespace FinanceManager.Api.Features.Labels.Services;

public interface ILabelSetterProgressTracker
{
    LabelSetterProgressSnapshot GetSnapshot();
    void StartJob(int accountId, int? userId, int totalEntries);
    void ReportBatchCompleted(int batchSize);
    void CompleteJob();
    void SetQueuedJobsCount(int count);
}