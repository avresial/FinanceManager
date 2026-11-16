using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// Account shell for the investment asset model. An investment account holds no per-account entries;
/// its holdings live in <see cref="InvestmentTransaction"/> rows and are valued on read through the
/// investment valuation/pricing services. The account record itself carries only identity/metadata.
/// </summary>
public class InvestmentAccount(int userId, int accountId, string name) : IAccount
{
    public int AccountId { get; set; } = accountId;
    public int UserId { get; set; } = userId;
    public string Name { get; set; } = name;

    // Retained on this wire model for compatibility; history bounds are not part of IAccount.
    public DateTime? Start { get; protected set; }
    public DateTime? End { get; protected set; }

}