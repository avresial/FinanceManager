namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// Classifies the kind of financial instrument an <see cref="Asset"/> represents.
/// </summary>
public enum AssetType
{
    Stock,
    ETF,
    Bond,
    MutualFund,
    Crypto,
    Cash,
    Other
}