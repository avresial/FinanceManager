namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>Portfolio-level cumulative time-weighted return result.</summary>
public sealed record TimeWeightedReturnResult(
    decimal? TotalReturn,
    TimeWeightedReturnStatus Status,
    DateTime StartDate,
    DateTime EndDate)
{
    public bool IsAvailable => Status == TimeWeightedReturnStatus.Available && TotalReturn.HasValue;
}