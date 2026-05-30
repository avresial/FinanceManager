namespace FinanceManager.Domain.Services;

/// <summary>
/// Per-account brute-force protection. Tracks consecutive failed logins for an account and locks it for a cooldown
/// window once a configured threshold is reached. Complements per-IP rate limiting, which cannot stop a targeted
/// attack that rotates source addresses against a single account.
/// </summary>
public interface IAccountLockoutService
{
    /// <summary>
    /// True when the account is currently locked out and the login must be refused without checking the password.
    /// </summary>
    Task<bool> IsLockedOut(string login, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed login attempt, locking the account for the configured window once the threshold is reached.
    /// Unknown accounts are ignored so the lockout state cannot be used to enumerate valid logins.
    /// </summary>
    Task RegisterFailedAttempt(string login, CancellationToken cancellationToken = default);

    /// <summary>Clears the failed-attempt counter and any active lockout after a successful authentication.</summary>
    Task Reset(string login, CancellationToken cancellationToken = default);
}