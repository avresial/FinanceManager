using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Identity.Users;

public class UsersService(IUserRepository userRepository, ICacheInvalidator cacheInvalidator, ILogger<UsersService> logger)
{
    public async Task<bool> DeleteUser(int userId, CancellationToken cancellationToken = default)
    {
        if (!await userRepository.RemoveUser(userId, cancellationToken)) return false;

        // The delete is committed, so a cache failure must not turn it into an error, and a cancelled request must still bust the cache.
        try
        {
            await cacheInvalidator.InvalidateUser(userId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Deleted user {UserId} but could not invalidate their cached data.", userId);
        }

        return true;
    }
}