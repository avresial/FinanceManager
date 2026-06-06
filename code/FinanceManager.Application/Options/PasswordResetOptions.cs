namespace FinanceManager.Application.Options;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>How long a freshly issued reset token stays valid. Defaults to 60 minutes.</summary>
    public int TokenValidityMinutes { get; set; } = 60;
}