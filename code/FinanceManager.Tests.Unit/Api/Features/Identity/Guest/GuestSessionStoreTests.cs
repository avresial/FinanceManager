
using FinanceManager.Api.Features.Identity.Guest;
namespace FinanceManager.Tests.Unit.Api.Features.Identity.Guest;

[Collection("Api")]
[Trait("Category", "Unit")]
public class GuestSessionStoreTests
{
    [Fact]
    public void CreateSession_MintsNegativeUniqueIds()
    {
        var store = new GuestSessionStore();

        var first = store.CreateSession();
        var second = store.CreateSession();

        Assert.True(first < 0);
        Assert.True(second < 0);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void IsActive_ReturnsTrueForKnownSessions_FalseAfterRemoval()
    {
        var store = new GuestSessionStore();
        var guestId = store.CreateSession();

        Assert.True(store.IsActive(guestId));
        Assert.True(store.Remove(guestId));
        Assert.False(store.IsActive(guestId));
    }

    [Fact]
    public void GetExpired_OnlyReturnsSessionsOlderThanTtl()
    {
        var store = new GuestSessionStore();
        var guestId = store.CreateSession();

        var freshLookup = store.GetExpired(TimeSpan.FromHours(1));
        Assert.DoesNotContain(guestId, freshLookup);

        // A non-positive ttl makes every existing session immediately expired.
        var expired = store.GetExpired(TimeSpan.Zero);
        Assert.Contains(guestId, expired);
    }
}