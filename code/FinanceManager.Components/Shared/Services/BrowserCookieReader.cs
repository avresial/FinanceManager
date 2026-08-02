using Microsoft.JSInterop;

namespace FinanceManager.Components.Shared.Services;

public sealed class BrowserCookieReader(IJSRuntime jsRuntime) : IBrowserCookieReader
{
    // Uses a dedicated existence check rather than testing getCookie's result for emptiness: a cookie written with
    // an empty value is still present, and callers asking "does it exist" must not be told otherwise.
    public Task<bool> Exists(string name, CancellationToken cancellationToken = default) =>
        jsRuntime.InvokeAsync<bool>("financeManager.hasCookie", cancellationToken, name).AsTask();
}