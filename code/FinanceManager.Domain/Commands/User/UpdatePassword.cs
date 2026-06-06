namespace FinanceManager.Application.Commands.User;

// CurrentPassword is verified server-side for self-service password changes. It is optional because an
// Admin resetting another user's password does not (and cannot) supply that user's current password.
public record UpdatePassword(int UserId, string Password, string? CurrentPassword = null);