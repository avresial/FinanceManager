namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>Explains whether a time-weighted return could be calculated.</summary>
public enum TimeWeightedReturnStatus
{
    Available,
    InsufficientData,
    Unavailable,
}