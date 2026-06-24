using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// Account shell for the investment asset model. An investment account holds no per-account entries;
/// its holdings live in <see cref="InvestmentTransaction"/> rows and are valued on read through the
/// investment valuation/pricing services. The account record itself carries only identity/metadata.
/// </summary>
public class InvestmentAccount : BasicAccountInformation
{
    public InvestmentAccount(int userId, int accountId, string name)
    {
        UserId = userId;
        AccountId = accountId;
        Name = name;
    }
}