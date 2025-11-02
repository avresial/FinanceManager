using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services;

public class UsersService(IFinancialAccountRepository financialAccountRepository, IUserRepository userRepository, ILogger<UsersService> logger)
{
    public async Task<bool> DeleteUser(int userId)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return false;

        foreach (var account in await financialAccountRepository.GetAvailableAccounts(userId))
        {
            try
            {
                await financialAccountRepository.RemoveAccount(account.Value, account.Key);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        return await userRepository.RemoveUser(userId);
    }
}
