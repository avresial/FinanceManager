namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// ETF/fund dividend handling: whether income is reinvested (accumulating) or paid out (distributing).
/// </summary>
public enum DistributionPolicy
{
    Accumulating,
    Distributing,
    Unknown
}