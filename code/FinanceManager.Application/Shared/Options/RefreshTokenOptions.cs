namespace FinanceManager.Application.Shared.Options;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>How long a refresh token stays valid, extended on each rotation. Defaults to 14 days.</summary>
    public int SlidingValidityDays { get; set; } = 14;

    /// <summary>Hard upper bound on a token family's lifetime regardless of rotation. Defaults to 30 days.</summary>
    public int AbsoluteValidityDays { get; set; } = 30;

    /// <summary>Name of the HttpOnly cookie that carries the refresh token.</summary>
    public string CookieName { get; set; } = "fm_refresh_token";
}