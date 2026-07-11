using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Application.FinancialAccounts.Shared.Imports;

// Shared validation for account imports: plan-limit and account-ownership checks
// that were previously duplicated across the currency, stock and bond import services.
public class ImportAccountValidator(IUserPlanVerifier userPlanVerifier)
{
    public async Task EnsureWithinPlanLimit(int userId, int entryCount)
    {
        if (!await userPlanVerifier.CanAddMoreEntries(userId, entryCount))
            throw new InvalidOperationException("Plan does not allow importing this many entries.");
    }

    public void EnsureOwnership(BasicAccountInformation? account, int userId)
    {
        if (account is null || account.UserId != userId)
            throw new InvalidOperationException("Account not found or access denied.");
    }
}