namespace FinanceManager.Application.Options;

public sealed class AccountLockoutOptions
{
    public const string SectionName = "AccountLockout";

    /// <summary>Master switch so the lockout can be disabled wholesale (e.g. in tests). Defaults to on.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failed login attempts that trips a lockout. Defaults to 5.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>How long an account stays locked once the threshold is hit. Defaults to 15 minutes.</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}