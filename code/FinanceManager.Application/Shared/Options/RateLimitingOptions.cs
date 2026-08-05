namespace FinanceManager.Application.Shared.Options;

/// <summary>
/// Configures ASP.NET Core rate limiting. A lenient <see cref="Global"/> limiter guards every request;
/// stricter <see cref="Auth"/> and <see cref="ExternalProvider"/> policies are layered on top of the
/// endpoints that are most attractive to abuse (credential stuffing, paid third-party quota exhaustion).
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Master switch. When false every limiter becomes a no-op (used by integration tests).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Baseline limit applied to every request regardless of endpoint.</summary>
    public RateLimitPolicyOptions Global { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    /// <summary>Tighter limit for authentication endpoints (login, refresh, logout) to blunt brute-force attempts.</summary>
    public RateLimitPolicyOptions Auth { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    /// <summary>Tighter limit for endpoints that fan out to paid/quota'd external providers (Alpha Vantage, OpenFIGI).</summary>
    public RateLimitPolicyOptions ExternalProvider { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
}

/// <summary>A single fixed-window rate-limit policy: at most <see cref="PermitLimit"/> requests per <see cref="WindowSeconds"/>.</summary>
public sealed class RateLimitPolicyOptions
{
    /// <summary>Maximum number of requests permitted inside one window.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>Length of the fixed window in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>How many requests may wait for the next window instead of being rejected outright. Defaults to none.</summary>
    public int QueueLimit { get; set; }
}