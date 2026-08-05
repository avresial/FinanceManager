namespace FinanceManager.Domain.Identity.Services;

/// <summary>
/// Outcome of a login attempt. Replaces the earlier <c>bool</c> return so callers can tell a genuine credential
/// failure apart from a rate-limit (429) or an account lockout (403) and surface the matching message.
/// </summary>
public sealed record LoginResult
{
    // No public primary constructor: static factories keep construction to the valid (status, payload) combinations
    // only — RetryAfter is meaningful only for RateLimited, Message only for LockedOut.
    private LoginResult(LoginResultStatus status, TimeSpan? retryAfter = null, string? message = null)
    {
        Status = status;
        RetryAfter = retryAfter;
        Message = message;
    }

    public LoginResultStatus Status { get; }

    /// <summary>How long to wait before retrying, from the response <c>Retry-After</c> header. Set only for <see cref="LoginResultStatus.RateLimited"/>.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Server-supplied explanation (e.g. the lockout reason). Set only for <see cref="LoginResultStatus.LockedOut"/>.</summary>
    public string? Message { get; }

    public bool IsSuccess => Status == LoginResultStatus.Success;

    public static LoginResult Success() => new(LoginResultStatus.Success);
    public static LoginResult InvalidCredentials() => new(LoginResultStatus.InvalidCredentials);
    public static LoginResult RateLimited(TimeSpan retryAfter) => new(LoginResultStatus.RateLimited, retryAfter);
    public static LoginResult LockedOut(string? message = null) => new(LoginResultStatus.LockedOut, message: message);
    public static LoginResult Error() => new(LoginResultStatus.Error);
}