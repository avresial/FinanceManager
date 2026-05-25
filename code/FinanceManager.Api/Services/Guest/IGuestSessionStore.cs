namespace FinanceManager.Api.Services.Guest;

public interface IGuestSessionStore
{
    int CreateSession();
    bool IsActive(int guestUserId);
    IReadOnlyCollection<int> GetExpired(TimeSpan ttl);
    bool Remove(int guestUserId);
}