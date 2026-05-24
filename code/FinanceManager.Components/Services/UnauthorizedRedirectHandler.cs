using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components;
using System.Net;

namespace FinanceManager.Components.Services;

/// <summary>
/// On a 401 response, clears the persisted user session and redirects to the login screen. Lets expired guest
/// tokens fall through silently instead of surfacing as broken-feature errors.
/// </summary>
public sealed class UnauthorizedRedirectHandler(
    NavigationManager navigation,
    ISessionStorageService sessionStorage,
    ILocalStorageService localStorage) : DelegatingHandler
{
    private const string _sessionKey = "userSession";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        // Login itself returns 403 on bad credentials, so any 401 here means a previously-issued token was rejected.
        await sessionStorage.RemoveItemAsync(_sessionKey, cancellationToken);
        await localStorage.RemoveItemAsync(_sessionKey, cancellationToken);
        navigation.NavigateTo("Login", forceLoad: false);

        return response;
    }
}
