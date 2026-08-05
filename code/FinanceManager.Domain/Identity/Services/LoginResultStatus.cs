namespace FinanceManager.Domain.Identity.Services;

/// <summary>
/// Discriminates the outcome of a login attempt so the UI can show the right message instead of collapsing
/// every failure into a credential error.
/// </summary>
public enum LoginResultStatus
{
    /// <summary>Credentials accepted and the session was established.</summary>
    Success,

    /// <summary>The username/password combination was rejected.</summary>
    InvalidCredentials,

    /// <summary>The request was throttled (HTTP 429); <see cref="LoginResult.RetryAfter"/> says how long to wait.</summary>
    RateLimited,

    /// <summary>The account is temporarily locked after repeated failed attempts (HTTP 403 with a message body).</summary>
    LockedOut,

    /// <summary>The login could not be completed for any other reason (server or network error).</summary>
    Error,
}