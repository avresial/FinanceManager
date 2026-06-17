namespace FinanceManager.Domain.Labels.Dtos;

public record LabelSetterProgressSnapshot(
    LabelSetterJobProgress? CurrentJob,
    int QueuedJobsCount);

public record LabelSetterJobProgress(
    int AccountId,
    int? UserId,
    int TotalEntries,
    int ProcessedEntries,
    DateTime StartedAtUtc);