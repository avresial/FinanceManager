using FinanceManager.Components.Shared.Services;
using System.Net;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

[Trait("Category", "Unit")]
public class PageActivityTrackerTests
{
    [Fact]
    public void IsIdle_UntilEveryActivityHasEnded()
    {
        var tracker = new PageActivityTracker();

        var first = tracker.Begin();
        var second = tracker.Begin();
        first.Dispose();
        Assert.False(tracker.IsIdle);

        second.Dispose();
        Assert.True(tracker.IsIdle);
    }

    [Fact]
    public void Dispose_TwiceEndsTheActivityOnce()
    {
        var tracker = new PageActivityTracker();
        var other = tracker.Begin();
        var activity = tracker.Begin();

        activity.Dispose();
        activity.Dispose();

        Assert.False(tracker.IsIdle);
        other.Dispose();
        Assert.True(tracker.IsIdle);
    }

    [Fact]
    public void Changed_RaisedOnBeginAndEnd()
    {
        var tracker = new PageActivityTracker();
        List<bool> idleStates = [];
        tracker.Changed += () => idleStates.Add(tracker.IsIdle);

        tracker.Begin().Dispose();

        Assert.Equal([false, true], idleStates);
    }

    [Fact]
    public async Task Handler_TracksTheRequestWhileItIsInFlight()
    {
        var tracker = new PageActivityTracker();
        bool? idleDuringRequest = null;
        var client = new HttpClient(new PageActivityHandler(tracker)
        {
            InnerHandler = new StubHandler(() =>
            {
                idleDuringRequest = tracker.IsIdle;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
        });

        await client.GetAsync("http://localhost/api/test", TestContext.Current.CancellationToken);

        Assert.False(idleDuringRequest);
        Assert.True(tracker.IsIdle);
    }

    [Fact]
    public async Task Handler_EndsTheActivityWhenTheRequestFails()
    {
        var tracker = new PageActivityTracker();
        var client = new HttpClient(new PageActivityHandler(tracker)
        {
            InnerHandler = new StubHandler(() => throw new HttpRequestException("offline")),
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://localhost/api/test", TestContext.Current.CancellationToken));

        Assert.True(tracker.IsIdle);
    }

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond());
    }
}