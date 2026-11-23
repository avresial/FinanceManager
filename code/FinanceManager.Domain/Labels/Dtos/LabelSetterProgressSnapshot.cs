namespace FinanceManager.Domain.Labels.Dtos;

public record LabelSetterProgressSnapshot(
    LabelSetterJobProgress? CurrentJob,
    int QueuedJobsCount);