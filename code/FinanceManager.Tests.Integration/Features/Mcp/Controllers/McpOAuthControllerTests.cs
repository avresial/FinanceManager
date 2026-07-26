using FinanceManager.Api.Features.Identity.Controllers;
using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.OAuth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Mcp.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public sealed class McpOAuthControllerTests : IDisposable
{
    private const int _userId = 7842;
    private const string _userLogin = "mcp-user@example.com";
    private readonly FinanceManagerApiTestApp _app;

    public McpOAuthControllerTests()
    {
        var user = new User
        {
            UserId = _userId,
            Login = _userLogin,
            UserRole = UserRole.User,
            CreationDate = DateTime.UtcNow
        };
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.GetUser(_userId)).ReturnsAsync(user);

        _app = new FinanceManagerApiTestApp(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddSingleton(users.Object);
        }, hostSettings: new Dictionary<string, string?>
        {
            ["UseInMemoryDatabase"] = "true"
        });

        using var scope = _app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<McpOAuthOptions>>().Value;
        scope.ServiceProvider.GetRequiredService<McpOAuthConfigurationReconciler>()
            .ReconcileAsync(options, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task AuthorizationCodeFlow_UsesBridgePkceRefreshRotationAndMcpResource()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var browser = CreateBrowser();
        var metadata = await browser.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", cancellationToken);
        Assert.EndsWith("/connect/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.EndsWith("/connect/token", metadata.GetProperty("token_endpoint").GetString());
        Assert.Equal(["S256"], metadata.GetProperty("code_challenge_methods_supported")
            .EnumerateArray().Select(value => value.GetString()));
        var csrfToken = await BootstrapCsrfToken(browser, cancellationToken);

        var authorizationUrl = AuthorizationUrl(
            "finance-manager-mcp-test",
            "http://127.0.0.1:6274/oauth/callback",
            includePkce: true,
            out var verifier);
        var anonymous = await browser.GetAsync(authorizationUrl, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("http://localhost/login?returnUrl=", anonymous.Headers.Location!.AbsoluteUri);

        var bridge = await browser.PostAsync(
            "/api/Auth/oauth-bridge", BridgeForm(IssueJwt(), authorizationUrl, csrfToken), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, bridge.StatusCode);
        Assert.Equal("/connect/authorize", bridge.Headers.Location!.AbsolutePath);
        var sessionCookie = Assert.Single(bridge.Headers.GetValues("Set-Cookie"));
        Assert.Contains("fm_mcp_authorization=", sessionCookie);
        Assert.Contains("path=/connect", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", sessionCookie, StringComparison.OrdinalIgnoreCase);

        var authorize = await browser.GetAsync(authorizationUrl, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var callback = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query);
        Assert.Equal("test-state", callback["state"]);
        var code = Assert.Single(callback["code"]);

        var tokenResponse = await ExchangeCode(
            browser,
            "finance-manager-mcp-test",
            "http://127.0.0.1:6274/oauth/callback",
            code!,
            verifier,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(token.GetProperty("access_token").GetString()));
        Assert.Contains("mcp", token.GetProperty("scope").GetString()!.Split(' '));
        var refreshToken = token.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        var refreshResponse = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "finance-manager-mcp-test",
            ["refresh_token"] = refreshToken!,
            ["resource"] = "http://localhost/mcp"
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.NotEqual(refreshToken, refreshed.GetProperty("refresh_token").GetString());

        var replay = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "finance-manager-mcp-test",
            ["refresh_token"] = refreshToken!,
            ["resource"] = "http://localhost/mcp"
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("invalid_grant", (await replay.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task ClientWithoutPkce_CanCompleteItsConfiguredFlow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var browser = CreateBrowser();
        var csrfToken = await BootstrapCsrfToken(browser, cancellationToken);
        var authorizationUrl = AuthorizationUrl(
            "finance-manager-mcp-test-no-pkce",
            "http://127.0.0.1:6274/oauth/no-pkce-callback",
            includePkce: false,
            out _);
        var bridge = await browser.PostAsync(
            "/api/Auth/oauth-bridge", BridgeForm(IssueJwt(), authorizationUrl, csrfToken), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, bridge.StatusCode);

        var authorize = await browser.GetAsync(authorizationUrl, cancellationToken);
        var code = Assert.Single(QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"]);
        var token = await ExchangeCode(
            browser,
            "finance-manager-mcp-test-no-pkce",
            "http://127.0.0.1:6274/oauth/no-pkce-callback",
            code!,
            verifier: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
    }

    [Fact]
    public async Task AuthorizationAndBridge_RejectInvalidInputsAndGuests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var browser = CreateBrowser();
        var csrfToken = await BootstrapCsrfToken(browser, cancellationToken);
        var valid = AuthorizationUrl(
            "finance-manager-mcp-test",
            "http://127.0.0.1:6274/oauth/callback",
            includePkce: true,
            out var verifier);

        var invalidResource = await browser.GetAsync(valid.Replace(
            Uri.EscapeDataString("http://localhost/mcp"),
            Uri.EscapeDataString("http://localhost/not-mcp"),
            StringComparison.Ordinal), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResource.StatusCode);
        Assert.Contains("invalid_target", await invalidResource.Content.ReadAsStringAsync(cancellationToken));

        var missingScope = await browser.GetAsync(
            valid.Replace("mcp%20offline_access", "offline_access", StringComparison.Ordinal), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, missingScope.StatusCode);
        Assert.Equal("invalid_scope", QueryHelpers.ParseQuery(missingScope.Headers.Location!.Query)["error"]);

        var invalidRedirect = await browser.GetAsync(valid.Replace(
            Uri.EscapeDataString("http://127.0.0.1:6274/oauth/callback"),
            Uri.EscapeDataString("http://127.0.0.1:6274/oauth/wrong"),
            StringComparison.Ordinal), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRedirect.StatusCode);

        var invalidClient = await browser.GetAsync(
            valid.Replace("finance-manager-mcp-test", "unknown-client", StringComparison.Ordinal), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidClient.StatusCode);

        var foreignBridge = await browser.PostAsync("/api/Auth/oauth-bridge",
            BridgeForm(IssueJwt(), "https://evil.example/connect/authorize", csrfToken), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, foreignBridge.StatusCode);

        var invalidToken = await browser.PostAsync(
            "/api/Auth/oauth-bridge", BridgeForm("invalid", valid, csrfToken), cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidToken.StatusCode);

        var guestToken = IssueJwt(isGuest: true);
        var guest = await browser.PostAsync(
            "/api/Auth/oauth-bridge", BridgeForm(guestToken, valid, csrfToken), cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, guest.StatusCode);

        var validBridge = await browser.PostAsync(
            "/api/Auth/oauth-bridge", BridgeForm(IssueJwt(), valid, csrfToken), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, validBridge.StatusCode);
        var authorize = await browser.GetAsync(valid, cancellationToken);
        var code = Assert.Single(QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"]);
        var invalidVerifier = await ExchangeCode(
            browser,
            "finance-manager-mcp-test",
            "http://127.0.0.1:6274/oauth/callback",
            code!,
            verifier + "wrong",
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidVerifier.StatusCode);
        Assert.Equal("invalid_grant",
            (await invalidVerifier.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Bridge_RequiresValidAntiforgeryToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var browser = CreateBrowser();
        var authorizationUrl = AuthorizationUrl(
            "finance-manager-mcp-test",
            "http://127.0.0.1:6274/oauth/callback",
            includePkce: true,
            out _);

        var missing = await browser.PostAsync(
            "/api/Auth/oauth-bridge", BridgeForm(IssueJwt(), authorizationUrl), cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);

        await BootstrapCsrfToken(browser, cancellationToken);
        var invalid = await browser.PostAsync(
            "/api/Auth/oauth-bridge", BridgeForm(IssueJwt(), authorizationUrl, "invalid"), cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode);
    }

    [Fact]
    public async Task Authorization_UsesForwardedHttpsSchemeBehindKnownProxy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var app = new FinanceManagerApiTestApp(hostSettings: new Dictionary<string, string?>
        {
            ["UseInMemoryDatabase"] = "true",
            ["McpOAuth:Issuer"] = "https://localhost/",
            ["McpOAuth:Resource"] = "https://localhost/mcp",
            ["McpOAuth:LoginUrl"] = "https://localhost/login"
        });
        using (var scope = app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<McpOAuthOptions>>().Value;
            await scope.ServiceProvider.GetRequiredService<McpOAuthConfigurationReconciler>()
                .ReconcileAsync(options, cancellationToken);
        }

        using var browser = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost/")
        });
        var authorizationUrl = AuthorizationUrl(
            "finance-manager-mcp-test",
            "http://127.0.0.1:6274/oauth/callback",
            includePkce: true,
            out _,
            resource: "https://localhost/mcp");
        using var request = new HttpRequestMessage(HttpMethod.Get, authorizationUrl);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        var response = await browser.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/login?returnUrl=https%3A%2F%2Flocalhost%2Fconnect%2Fauthorize",
            response.Headers.Location!.AbsoluteUri);
    }

    public void Dispose()
    {
        _app.Dispose();
    }

    private HttpClient CreateBrowser() => _app.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("http://localhost/")
    });

    private string IssueJwt(bool isGuest = false)
    {
        using var scope = _app.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<JwtTokenGenerator>()
            .GenerateToken(_userLogin, isGuest ? -1 : _userId, UserRole.User, isGuest)
            .AccessToken;
    }

    private static async Task<string> BootstrapCsrfToken(HttpClient browser, CancellationToken cancellationToken)
    {
        var response = await browser.GetAsync("/api/Auth/csrf-token", cancellationToken);
        response.EnsureSuccessStatusCode();
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : throw new InvalidOperationException("CSRF endpoint did not return cookies.");
        var cookie = cookies.Single(value => value.StartsWith(
            $"{AuthController.CsrfTokenCookieName}=", StringComparison.OrdinalIgnoreCase));
        return WebUtility.UrlDecode(cookie.Split(';', 2)[0]
            .Substring(AuthController.CsrfTokenCookieName.Length + 1));
    }

    private static FormUrlEncodedContent BridgeForm(
        string token,
        string returnUrl,
        string? csrfToken = null) => new(new Dictionary<string, string>
        {
            ["token"] = token,
            ["returnUrl"] = returnUrl.StartsWith("/", StringComparison.Ordinal)
            ? "http://localhost" + returnUrl
            : returnUrl,
            ["__RequestVerificationToken"] = csrfToken ?? string.Empty
        });

    private static string AuthorizationUrl(
        string clientId,
        string redirectUri,
        bool includePkce,
        out string? verifier,
        string resource = "http://localhost/mcp")
    {
        var values = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = "mcp offline_access",
            ["resource"] = resource,
            ["state"] = "test-state"
        };
        verifier = null;
        if (includePkce)
        {
            verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            values["code_challenge"] = WebEncoders.Base64UrlEncode(
                SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            values["code_challenge_method"] = "S256";
        }

        return QueryHelpers.AddQueryString("/connect/authorize", values);
    }

    private static Task<HttpResponseMessage> ExchangeCode(
        HttpClient browser,
        string clientId,
        string redirectUri,
        string code,
        string? verifier,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["resource"] = "http://localhost/mcp"
        };
        if (verifier is not null)
            values["code_verifier"] = verifier;

        return browser.PostAsync("/connect/token", new FormUrlEncodedContent(values), cancellationToken);
    }
}