namespace FinanceManager.Domain.Labels.Commands;

public record UpdateRecurringSubscription(
    bool IsMuted,
    bool IsCancelled,
    bool IsFlaggedForReview);