using FinanceManager.Api.Hubs;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Labels.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Services;

public sealed class LabelSetterProgressTracker(IHubContext<LabelSetterProgressHub> hubContext) : ILabelSetterProgressTracker
{
    private readonly Lock _lock = new();
    private LabelSetterJobProgress? _currentJob;
    private int _queuedJobsCount;

    public LabelSetterProgressSnapshot GetSnapshot()
    {
        lock (_lock)
            return new LabelSetterProgressSnapshot(_currentJob, _queuedJobsCount);
    }

    public void StartJob(int accountId, int? userId, int totalEntries)
    {
        lock (_lock)
        {
            _currentJob = new LabelSetterJobProgress(accountId, userId, totalEntries, 0, DateTime.UtcNow);
        }
        Broadcast();
    }

    public void ReportBatchCompleted(int batchSize)
    {
        lock (_lock)
        {
            if (_currentJob is null) return;
            var processed = Math.Min(_currentJob.ProcessedEntries + batchSize, _currentJob.TotalEntries);
            _currentJob = _currentJob with { ProcessedEntries = processed };
        }
        Broadcast();
    }

    public void CompleteJob()
    {
        lock (_lock)
        {
            _currentJob = null;
        }
        Broadcast();
    }

    public void SetQueuedJobsCount(int count)
    {
        lock (_lock)
        {
            _queuedJobsCount = count;
        }
        Broadcast();
    }

    private void Broadcast()
    {
        var snapshot = GetSnapshot();
        _ = hubContext.Clients.Group(LabelSetterProgressHub.GroupName).SendAsync("ProgressUpdated", snapshot);
    }
}