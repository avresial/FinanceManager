namespace FinanceManager.Domain.Labels.Dtos;

public record LabelSetterJobProgress(
    int UserId,
    int TotalEntries,
    int ProcessedEntries,
    DateTime StartedAtUtc);