using FinanceManager.Application.Options;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Options;

namespace FinanceManager.Application.Services;

/// <summary>
/// Persists consecutive failed-login counters on the account itself and locks the account for a fixed cooldown
/// window once <see cref="AccountLockoutOptions.MaxFailedAttempts"/> is reached. A lapsed lockout starts a fresh
/// count, and a successful login clears the state.
/// </summary>
public class AccountLockoutService(IUserRepository userRepository, IOptions<AccountLockoutOptions> options) : IAccountLockoutService
{
    private readonly AccountLockoutOptions _options = options.Value;

    public async Task<bool> IsLockedOut(string login, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return false;

        var state = await userRepository.GetLoginThrottlingState(login);
        return state?.LockoutEndUtc is { } lockoutEnd && lockoutEnd > DateTime.UtcNow;
    }

    public async Task RegisterFailedAttempt(string login, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var state = await userRepository.GetLoginThrottlingState(login);
        if (state is null) return; // Unknown account — nothing to track, and tracking it would leak its existence.

        var now = DateTime.UtcNow;

        // An expired lockout means the previous strike count is stale, so start counting again from zero.
        var priorAttempts = state.LockoutEndUtc is { } lockoutEnd && lockoutEnd <= now ? 0 : state.FailedAttempts;
        var attempts = priorAttempts + 1;

        DateTime? newLockoutEnd = attempts >= _options.MaxFailedAttempts ? now.Add(_options.LockoutDuration) : null;

        await userRepository.SetLoginThrottlingState(login, attempts, newLockoutEnd);
    }

    public async Task Reset(string login, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var state = await userRepository.GetLoginThrottlingState(login);
        if (state is null || (state.FailedAttempts == 0 && state.LockoutEndUtc is null)) return; // Already clean.

        await userRepository.SetLoginThrottlingState(login, 0, null);
    }
}