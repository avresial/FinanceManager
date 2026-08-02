using Microsoft.JSInterop;

namespace FinanceManager.Components.Shared.Services;

public sealed class BrowserCookieReader(IJSRuntime jsRuntime) : IBrowserCookieReader
{
    public async Task<bool> Exists(string name, CancellationToken cancellationToken = default)
    {
        var value = await jsRuntime.InvokeAsync<string?>("financeManager.getCookie", cancellationToken, name);
        return !string.IsNullOrEmpty(value);
    }
}