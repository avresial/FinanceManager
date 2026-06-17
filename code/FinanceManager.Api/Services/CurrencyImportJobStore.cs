using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using System.Collections.Concurrent;

namespace FinanceManager.Api.Services;

public sealed class CurrencyImportJobStore : ICurrencyImportJobStore
{
    private static readonly TimeSpan _completedJobRetention = TimeSpan.FromHours(5);
    private static readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<Guid, CurrencyImportJobState> _jobs = new();
    private readonly object _cleanupGate = new();
    private DateTime _lastCleanupUtc = DateTime.MinValue;

    public Guid CreateJob(int userId, int accountId, int totalEntries)
    {
        CleanupExpiredCompletedJobs();

        var jobId = Guid.NewGuid();
        var state = new CurrencyImportJobState(jobId, userId, accountId, totalEntries);
        _jobs[jobId] = state;
        return jobId;
    }

    public bool TrySetRunning(Guid jobId)
    {
        CleanupExpiredCompletedJobs();

        if (!_jobs.TryGetValue(jobId, out var state))
            return false;

        lock (state.Gate)
        {
            state.Status = AsyncImportJobState.Running;
            state.LastUpdatedUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool TryUpdateProgress(Guid jobId, int processedEntries, int imported, int failed)
    {
        CleanupExpiredCompletedJobs();

        if (!_jobs.TryGetValue(jobId, out var state))
            return false;

        lock (state.Gate)
        {
            state.ProcessedEntries = processedEntries;
            state.Imported = imported;
            state.Failed = failed;
            state.LastUpdatedUtc = DateTime.UtcNow;
            return true;
        }
    }

    public ImportJobConflict? TryAddConflict(Guid jobId, ImportConflict conflict)
    {
        CleanupExpiredCompletedJobs();

        if (!_jobs.TryGetValue(jobId, out var state))
            return null;

        lock (state.Gate)
        {
            state.NextConflictId++;
            var conflictId = $"{jobId:N}-{state.NextConflictId}";
            var withId = conflict with { ConflictId = conflictId };
            var importJobConflict = new ImportJobConflict(conflictId, withId, withId.IsExactMatch);
            state.Conflicts.Add(importJobConflict);
            if (IsActionable(importJobConflict))
                state.Status = AsyncImportJobState.WaitingForResolution;
            state.LastUpdatedUtc = DateTime.UtcNow;
            return importJobConflict;
        }
    }

    public bool TryMarkCompleted(Guid jobId, ImportResult result)
    {
        CleanupExpiredCompletedJobs();

        if (!_jobs.TryGetValue(jobId, out var state))
            return false;

        lock (state.Gate)
        {
            state.ProcessedEntries = state.TotalEntries;
            state.Imported = result.Imported;
            state.Failed = result.Failed;
            state.Errors.Clear();
            state.Errors.AddRange(result.Errors);
            state.Status = state.Conflicts.Any(IsActionableUnresolved)
                ? AsyncImportJobState.CompletedWithConflicts
                : AsyncImportJobState.Completed;
            state.LastUpdatedUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool TryMarkFailed(Guid jobId, string error)
    {
        CleanupExpiredCompletedJobs();

        if (!_jobs.TryGetValue(jobId, out var state))
            return false;

        lock (state.Gate)
        {
            state.Status = AsyncImportJobState.Failed;
            state.Errors.Add(error);
            state.LastUpdatedUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool TryGetStatus(Guid jobId, int userId, out CurrencyImportJobStatusDto? status)
    {
        CleanupExpiredCompletedJobs();

        status = null;

        if (!_jobs.TryGetValue(jobId, out var state) || state.UserId != userId)
            return false;

        lock (state.Gate)
        {
            status = ToDto(state);
            return true;
        }
    }

    public bool CanAccessJob(Guid jobId, int userId) =>
        CanAccessJobInternal(jobId, userId);

    public bool TryResolveConflict(Guid jobId, int userId, CurrencyImportConflictResolutionDecisionDto decision, out ResolvedImportConflict? resolvedConflict)
    {
        CleanupExpiredCompletedJobs();

        resolvedConflict = null;

        if (!_jobs.TryGetValue(jobId, out var state) || state.UserId != userId)
            return false;

        lock (state.Gate)
        {
            var index = state.Conflicts.FindIndex(x => x.ConflictId == decision.ConflictId);
            if (index < 0)
                return false;

            var current = state.Conflicts[index];
            if (current.IsResolved)
                return true;

            var conflict = current.Conflict;
            if (decision.PickImported)
            {
                if (conflict.ImportEntry is null && conflict.ExistingEntry is null)
                    return false;

                resolvedConflict = new ResolvedImportConflict(
                    conflict.AccountId,
                    conflict.ImportEntry is not null,
                    conflict.ImportEntry,
                    false,
                    conflict.ExistingEntry?.EntryId);
            }
            else if (decision.PickExisting)
            {
                if (conflict.ImportEntry is null && conflict.ExistingEntry is null)
                    return false;

                resolvedConflict = new ResolvedImportConflict(
                    conflict.AccountId,
                    false,
                    conflict.ImportEntry,
                    true,
                    conflict.ExistingEntry?.EntryId);
            }
            else
            {
                return false;
            }

            state.Conflicts[index] = current with { IsResolved = true };
            state.LastUpdatedUtc = DateTime.UtcNow;

            if (state.Status == AsyncImportJobState.WaitingForResolution && state.Conflicts.All(x => !IsActionable(x) || x.IsResolved))
                state.Status = state.ProcessedEntries >= state.TotalEntries
                    ? AsyncImportJobState.Completed
                    : AsyncImportJobState.Running;

            return true;
        }
    }

    private bool CanAccessJobInternal(Guid jobId, int userId)
    {
        CleanupExpiredCompletedJobs();
        return _jobs.TryGetValue(jobId, out var state) && state.UserId == userId;
    }

    private void CleanupExpiredCompletedJobs()
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanupUtc < _cleanupInterval)
            return;

        lock (_cleanupGate)
        {
            now = DateTime.UtcNow;
            if (now - _lastCleanupUtc < _cleanupInterval)
                return;

            _lastCleanupUtc = now;
            var expireBefore = now - _completedJobRetention;

            foreach (var pair in _jobs)
            {
                var shouldRemove = false;
                lock (pair.Value.Gate)
                {
                    shouldRemove = IsTerminalState(pair.Value.Status) && pair.Value.LastUpdatedUtc <= expireBefore;
                }

                if (shouldRemove)
                    _jobs.TryRemove(pair.Key, out _);
            }
        }
    }

    private static bool IsTerminalState(AsyncImportJobState status) =>
        status is AsyncImportJobState.Completed or AsyncImportJobState.CompletedWithConflicts or AsyncImportJobState.Failed;

    private static CurrencyImportJobStatusDto ToDto(CurrencyImportJobState state)
    {
        var isCompleted = state.Status is AsyncImportJobState.Completed or AsyncImportJobState.CompletedWithConflicts or AsyncImportJobState.Failed;
        var actionableConflicts = state.Conflicts.Where(IsActionable).ToList();
        var resolvedCount = actionableConflicts.Count(x => x.IsResolved);

        return new CurrencyImportJobStatusDto(
            state.JobId,
            state.Status,
            state.TotalEntries,
            state.ProcessedEntries,
            state.Imported,
            state.Failed,
            actionableConflicts.Count,
            resolvedCount,
            state.Errors.ToList(),
            state.Conflicts.ToList(),
            isCompleted);
    }

    private static bool IsActionable(ImportJobConflict conflict) => !conflict.Conflict.IsExactMatch;

    private static bool IsActionableUnresolved(ImportJobConflict conflict) => IsActionable(conflict) && !conflict.IsResolved;

    private sealed class CurrencyImportJobState(Guid jobId, int userId, int accountId, int totalEntries)
    {
        public Guid JobId { get; } = jobId;
        public int UserId { get; } = userId;
        public int AccountId { get; } = accountId;
        public int TotalEntries { get; } = totalEntries;

        public object Gate { get; } = new();

        public AsyncImportJobState Status { get; set; } = AsyncImportJobState.Queued;
        public int ProcessedEntries { get; set; }
        public int Imported { get; set; }
        public int Failed { get; set; }
        public int NextConflictId { get; set; }
        public List<string> Errors { get; } = [];
        public List<ImportJobConflict> Conflicts { get; } = [];
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}