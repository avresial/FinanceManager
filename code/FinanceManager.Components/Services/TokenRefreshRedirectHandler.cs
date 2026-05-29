using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace FinanceManager.Components.Services;

/// <summary>
/// On a 401 response, transparently tries to mint a new access token from the refresh-token cookie and replays the
/// original request once. Only when the refresh itself fails does it clear the session, flag it as expired, and
/// redirect to the login screen — so a routinely expired 15-minute access token never surfaces as a broken feature.
/// </summary>
public sealed class TokenRefreshRedirectHandler(
    IServiceProvider services,
    NavigationManager navigation,
    ISessionStorageService sessionStorage,
    ILocalStorageService localStorage) : DelegatingHandler
{
    private const string _sessionKey = "userSession";
    public const string SessionExpiredKey = "sessionExpired";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer the body up front so the request can be replayed verbatim after a refresh (the stream is otherwise
        // consumed by the first send). Bodies here are small JSON payloads, so the cost is negligible.
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        // The auth endpoints carry their own 401 semantics (bad credentials / expired refresh). Never try to
        // refresh in response to them, or we'd recurse.
        if (IsAuthEndpoint(request.RequestUri)) return response;

        // Resolved lazily (not via the constructor) to avoid a DI cycle: the login service depends on the very
        // HttpClient this handler is wired into.
        var loginService = services.GetRequiredService<ILoginService>();

        if (await loginService.TryRefresh())
        {
            var token = (await loginService.GetLoggedUser())?.Token;
            var retry = await CloneRequestAsync(request, token, cancellationToken);
            response.Dispose();
            return await base.SendAsync(retry, cancellationToken);
        }

        // Refresh failed → the session is genuinely over. Clean up, leave a breadcrumb for the login page to show a
        // "session expired" message, and redirect.
        await sessionStorage.RemoveItemAsync(_sessionKey, cancellationToken);
        await localStorage.RemoveItemAsync(_sessionKey, cancellationToken);
        await localStorage.SetItemAsync(SessionExpiredKey, true, cancellationToken);
        navigation.NavigateTo("Login", forceLoad: false);

        return response;
    }

    private static bool IsAuthEndpoint(Uri? uri)
    {
        if (uri is null) return false;

        var path = uri.AbsolutePath;
        return path.EndsWith("/api/Auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/api/Auth/logout", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/api/Login", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, string? token, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        if (request.Content is not null)
        {
            var buffer = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var content = new ByteArrayContent(buffer);
            foreach (var header in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }

        foreach (var header in request.Headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase)) continue;
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(token))
            clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        foreach (var option in request.Options)
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;

        return clone;
    }
}