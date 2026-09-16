namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>Explains whether portfolio return attribution could be calculated.</summary>
public enum PortfolioReturnAttributionStatus
{
    Available,
    InsufficientData,
    Unavailable,
}