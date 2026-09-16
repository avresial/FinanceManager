namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>Explains whether a money-weighted return could be calculated.</summary>
public enum MoneyWeightedReturnStatus
{
    Available,
    InsufficientData,
    Unavailable,
    NoSolution,
}