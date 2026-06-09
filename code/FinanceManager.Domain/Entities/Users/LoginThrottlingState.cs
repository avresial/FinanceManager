namespace FinanceManager.Domain.Entities.Users;

/// <summary>
/// Snapshot of an account's brute-force throttling state: how many consecutive failed logins have been recorded
/// and, when the threshold has been reached, the UTC instant the lockout expires.
/// </summary>
public record LoginThrottlingState(int FailedAttempts, DateTime? LockoutEndUtc);