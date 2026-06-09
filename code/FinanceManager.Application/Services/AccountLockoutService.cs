using FinanceManager.Application.Options;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Options;

namespace FinanceManager.Application.Services;

/// <summary>
/// Locks an account for a fixed cooldown window once <see cref="AccountLockoutOptions.MaxFailedAttempts"/>
/// consecutive failures are reached. The failed-attempt counter is incremented atomically in the repository so
/// concurrent guesses can't lose updates; locking the account also resets the counter, so a lapsed lockout requires
/// a fresh threshold of failures to lock again, and a successful login clears the state.
/// </summary>
public class AccountLockoutService(IUserRepository userRepository, IOptions<AccountLockoutOptions> options) : IAccountLockoutService
{
    private readonly AccountLockoutOptions _options = options.Value;

    public async Task<bool> IsLockedOut(string login, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return false;

        var state = await userRepository.GetLoginThrottlingState(login);
        return state?.LockoutEndUtc is DateTime lockoutEnd && lockoutEnd > DateTime.UtcNow;
    }

    public async Task RegisterFailedAttempt(string login, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        // Increment first (atomic), then decide on the lockout from the resulting count. Unknown accounts return
        // null and are ignored, so the lockout state can't be used to enumerate valid logins.
        var attempts = await userRepository.IncrementFailedLoginAttempts(login);
        if (attempts is null) return;

        if (attempts >= _options.MaxFailedAttempts)
            await userRepository.LockAccount(login, DateTime.UtcNow.Add(_options.LockoutDuration));
    }

    public async Task Reset(string login, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        // Skip the write on the common case of a clean account so a normal login doesn't pay for a pointless update.
        var state = await userRepository.GetLoginThrottlingState(login);
        if (state is null || (state.FailedAttempts == 0 && state.LockoutEndUtc is null)) return;

        await userRepository.ResetLoginThrottling(login);
    }
}