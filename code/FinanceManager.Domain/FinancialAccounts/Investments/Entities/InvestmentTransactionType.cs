namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// The direction of an <see cref="InvestmentTransaction"/>. Kept minimal (buy/sell) for now;
/// dividends, splits, transfers and tax events are intentionally out of scope.
/// </summary>
public enum InvestmentTransactionType
{
    Buy,
    Sell
}