using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Identity.Services;

/// <summary>Counts GET requests per path and can hold responses back until a gate opens.</summary>
internal sealed class RequestCountingHandler(Func<string, object> payloadFor, Task? gate = null) : HttpMessageHandler
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    public int GetCount(string path) => _counts.TryGetValue(path, out var count) ? count : 0;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        _counts[path] = GetCount(path) + 1;

        if (gate is not null)
            await gate;

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payloadFor(path)) };
    }
}