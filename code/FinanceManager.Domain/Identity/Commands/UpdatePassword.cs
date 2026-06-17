using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Identity.Commands;

// CurrentPassword is verified server-side for self-service password changes. It is optional because an
// Admin resetting another user's password does not (and cannot) supply that user's current password.
public record UpdatePassword(
    [Range(1, int.MaxValue)] int UserId,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [StringLength(128, MinimumLength = 8)] string? CurrentPassword = null);