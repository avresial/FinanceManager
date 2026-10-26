namespace FinanceManager.Components.Shared.Services;

/// <summary>Reports each request of the app <see cref="HttpClient"/> to the <see cref="PageActivityTracker"/>.</summary>
public sealed class PageActivityHandler(PageActivityTracker tracker) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var activity = tracker.Begin();
        return await base.SendAsync(request, cancellationToken);
    }
}
