namespace FinanceManager.Domain.Services;

public interface IGuestSessionAccessor
{
    bool IsGuest { get; }
    int? GuestUserId { get; }

    /// <summary>
    /// Pins this scope to a guest user for code paths that run before the JWT exists (e.g. seeding inside the login flow).
    /// </summary>
    void SetGuestUserId(int guestUserId);
}