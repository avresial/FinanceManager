namespace FinanceManager.Domain.Commands.User;

/// <summary>
/// Response to a forgot-password request. <see cref="Message"/> is intentionally identical whether or not the
/// account exists, so the response leaks no account-enumeration signal.
/// <para>
/// <see cref="ResetToken"/> is a TEMPORARY shortcut for the period before a transactional email provider is wired
/// up (issue #280): with no email being sent, the client needs the token to show a direct "reset password" link.
/// It is always populated — a real persisted token for registered accounts and a throwaway, non-persisted one for
/// unknown emails — so its presence reveals nothing about whether the account exists (#342). Once email delivery
/// lands this field must be removed so the token is only ever delivered out-of-band via the emailed link.
/// </para>
/// </summary>
public record ForgotPasswordResponse(string Message, string? ResetToken = null);