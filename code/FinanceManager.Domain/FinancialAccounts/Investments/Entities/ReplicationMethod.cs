namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// ETF replication method describing how the fund tracks its benchmark index.
/// </summary>
public enum ReplicationMethod
{
    Physical,
    Sampling,
    Synthetic,
    Unknown
}