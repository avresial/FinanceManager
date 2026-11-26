using FinanceManager.Application.Identity.Users;

namespace FinanceManager.Application.Transactions.Import;

// Cash and bond imports share the owner’s entry limit.
public class ImportEntryLimitValidator(IUserPlanVerifier userPlanVerifier)
{
    public async Task EnsureWithinPlanLimit(int userId, int entryCount)
    {
        if (!await userPlanVerifier.CanAddMoreEntries(userId, entryCount))
            throw new InvalidOperationException("Plan does not allow importing this many entries.");
    }
}