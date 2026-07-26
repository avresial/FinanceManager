using Microsoft.JSInterop;

namespace FinanceManager.Components.Shared.Services;

public interface IAntiforgeryTokenService
{
    public const string HeaderName = "X-CSRF-Token";

    Task<HttpRequestMessage> CreateRequest(HttpMethod method, string requestUri, CancellationToken cancellationToken);

    void ClearToken();
}

public sealed class AntiforgeryTokenService(HttpClient httpClient, IJSRuntime jsRuntime) : IAntiforgeryTokenService
{
    public const string CookieName = "fm_csrf";

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;

    public async Task<HttpRequestMessage> CreateRequest(HttpMethod method, string requestUri, CancellationToken cancellationToken)
    {
        var token = await GetToken(cancellationToken);

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.TryAddWithoutValidation(IAntiforgeryTokenService.HeaderName, token);
        return request;
    }

    public void ClearToken() => _cachedToken = null;

    private async Task<string> GetToken(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken))
            return _cachedToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken))
                return _cachedToken;

            using var response = await httpClient.GetAsync("api/Auth/csrf-token", cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await jsRuntime.InvokeAsync<string>("financeManager.getCookie", cancellationToken, CookieName);
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("The antiforgery token cookie was not issued.");

            _cachedToken = token;
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}