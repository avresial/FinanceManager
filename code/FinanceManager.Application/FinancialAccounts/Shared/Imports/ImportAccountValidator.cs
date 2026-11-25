using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Application.FinancialAccounts.Shared.Imports;

// Cash and bond imports share the owner’s entry limit.
public class ImportAccountValidator(IUserPlanVerifier userPlanVerifier)
{
    public async Task EnsureWithinPlanLimit(int userId, int entryCount)
    {
        if (!await userPlanVerifier.CanAddMoreEntries(userId, entryCount))
            throw new InvalidOperationException("Plan does not allow importing this many entries.");
    }
}