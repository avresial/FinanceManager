namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>Portfolio-level annualized money-weighted return (XIRR) result.</summary>
public sealed record MoneyWeightedReturnResult(
    decimal? AnnualizedReturn,
    MoneyWeightedReturnStatus Status,
    DateTime StartDate,
    DateTime EndDate)
{
    public bool IsAvailable => Status == MoneyWeightedReturnStatus.Available && AnnualizedReturn.HasValue;
}