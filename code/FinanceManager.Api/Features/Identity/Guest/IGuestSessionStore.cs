namespace FinanceManager.Api.Features.Identity.Guest;

public interface IGuestSessionStore
{
    int CreateSession();
    bool IsActive(int guestUserId);
    IReadOnlyCollection<int> GetExpired(TimeSpan ttl);
    bool Remove(int guestUserId);
}