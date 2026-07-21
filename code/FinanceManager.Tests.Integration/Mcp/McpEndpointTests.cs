using FinanceManager.Api.Controllers;
using FinanceManager.Api.Services;
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
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Mcp;

[Collection("api")]
[Trait("Category", "Integration")]
public sealed class McpEndpointTests : IDisposable
{
    private const int _userId = 8459;
    private const string _userLogin = "mcp-protocol-user@example.com";
    private readonly FinanceManagerApiTestApp _app;

    public McpEndpointTests()
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
        });

        using var scope = _app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<McpOAuthOptions>>().Value;
        scope.ServiceProvider.GetRequiredService<McpOAuthConfigurationReconciler>()
            .ReconcileAsync(options, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task DiscoveryDocuments_DescribeProtectedMcpResourceAndOAuthServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();

        var discovery = await client.GetFromJsonAsync<JsonElement>("/.well-known/mcp.json", cancellationToken);
        Assert.Equal("Finance Manager MCP", discovery.GetProperty("name").GetString());
        Assert.Equal("http://localhost/mcp", discovery.GetProperty("mcp_endpoint").GetString());
        Assert.Equal("http://localhost/", discovery.GetProperty("authorization_server").GetString());
        Assert.Equal("http://localhost/connect/authorize", discovery.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("http://localhost/connect/token", discovery.GetProperty("token_endpoint").GetString());
        Assert.Equal("finance-manager-mcp-test", discovery.GetProperty("client_id").GetString());
        Assert.Contains("mcp", discovery.GetProperty("scopes_supported").EnumerateArray().Select(value => value.GetString()));

        var protectedResource = await client.GetFromJsonAsync<JsonElement>(
            "/.well-known/oauth-protected-resource/mcp", cancellationToken);
        Assert.Equal("http://localhost/mcp", protectedResource.GetProperty("resource").GetString());
        Assert.Contains("http://localhost/", protectedResource.GetProperty("authorization_servers")
            .EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("mcp", protectedResource.GetProperty("scopes_supported")
            .EnumerateArray().Select(value => value.GetString()));

        var authorizationServer = await client.GetFromJsonAsync<JsonElement>(
            "/.well-known/openid-configuration", cancellationToken);
        Assert.Equal("http://localhost/connect/authorize", authorizationServer.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("http://localhost/connect/token", authorizationServer.GetProperty("token_endpoint").GetString());
        Assert.Contains("S256", authorizationServer.GetProperty("code_challenge_methods_supported")
            .EnumerateArray().Select(value => value.GetString()));

        var oauthAuthorizationServer = await client.GetFromJsonAsync<JsonElement>(
            "/.well-known/oauth-authorization-server", cancellationToken);
        Assert.Equal("http://localhost/connect/authorize", oauthAuthorizationServer.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("http://localhost/connect/token", oauthAuthorizationServer.GetProperty("token_endpoint").GetString());
    }

    [Fact]
    public async Task McpWithoutValidToken_ReturnsChallengePointingToResourceMetadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        using var request = McpRequest("tools/list", 1, new { });
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header =>
            string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
            header.Parameter?.Contains("resource_metadata=", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task AuthenticatedClient_CanInitializeListToolsAndCallWhoAmI()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var accessToken = await GetAccessToken(client, cancellationToken);

        using var initializeRequest = McpRequest("initialize", 1, new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "FinanceManager integration tests", version = "1.0" }
        }, accessToken);
        var initialize = await client.SendAsync(initializeRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        var initializeJson = await ReadMcpJson(initialize, cancellationToken);
        Assert.Equal("2.0", initializeJson.GetProperty("jsonrpc").GetString());
        Assert.Equal("2025-06-18", initializeJson.GetProperty("result").GetProperty("protocolVersion").GetString());

        using var listRequest = McpRequest("tools/list", 2, new { }, accessToken);
        listRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-06-18");
        var list = await client.SendAsync(listRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listJson = await ReadMcpJson(list, cancellationToken);
        var tools = listJson.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        Assert.Contains(tools, tool => tool.GetProperty("name").GetString() == "who_am_i");

        using var callRequest = McpRequest("tools/call", 3, new
        {
            name = "who_am_i",
            arguments = new { }
        }, accessToken);
        callRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-06-18");
        var call = await client.SendAsync(callRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, call.StatusCode);
        var callJson = await ReadMcpJson(call, cancellationToken);
        var resultText = callJson.GetProperty("result").GetRawText();
        Assert.Contains(_userId.ToString(), resultText, StringComparison.Ordinal);
        Assert.Contains(_userLogin, resultText, StringComparison.Ordinal);
        Assert.Contains("User", resultText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledFeature_DoesNotPublishMcpOrOAuthEndpoints()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var app = new FinanceManagerApiTestApp(hostSettings: new Dictionary<string, string?>
        {
            ["McpOAuth:Enabled"] = "false"
        });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost/")
        });

        foreach (var path in new[]
                 {
                     "/.well-known/mcp.json",
                     "/.well-known/oauth-protected-resource/mcp",
                     "/.well-known/openid-configuration",
                     "/.well-known/oauth-authorization-server"
                 })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, cancellationToken)).StatusCode);
        }

        using var request = McpRequest("tools/list", 1, new { });
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request, cancellationToken)).StatusCode);
    }

    public void Dispose() => _app.Dispose();

    private HttpClient CreateBrowser() => _app.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("http://localhost/")
    });

    private async Task<string> GetAccessToken(HttpClient client, CancellationToken cancellationToken)
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizationUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "finance-manager-mcp-test",
            ["response_type"] = "code",
            ["redirect_uri"] = "http://127.0.0.1:6274/oauth/callback",
            ["scope"] = "mcp offline_access",
            ["resource"] = "http://localhost/mcp",
            ["state"] = "mcp-protocol-test",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        });
        var csrfToken = await BootstrapCsrfToken(client, cancellationToken);
        var bridge = await client.PostAsync("/api/Auth/oauth-bridge", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = IssueJwt(),
            ["returnUrl"] = "http://localhost" + authorizationUrl,
            ["__RequestVerificationToken"] = csrfToken
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, bridge.StatusCode);

        var authorize = await client.GetAsync(authorizationUrl, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var code = Assert.Single(QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"]);
        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "finance-manager-mcp-test",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1:6274/oauth/callback",
            ["resource"] = "http://localhost/mcp",
            ["code_verifier"] = verifier
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return token.GetProperty("access_token").GetString()!;
    }

    private string IssueJwt()
    {
        using var scope = _app.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<JwtTokenGenerator>()
            .GenerateToken(_userLogin, _userId, UserRole.User, false)
            .AccessToken;
    }

    private static async Task<string> BootstrapCsrfToken(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync("/api/Auth/csrf-token", cancellationToken);
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith($"{AuthController.CsrfTokenCookieName}=", StringComparison.OrdinalIgnoreCase));
        return WebUtility.UrlDecode(cookie.Split(';', 2)[0][(AuthController.CsrfTokenCookieName.Length + 1)..]);
    }

    private static HttpRequestMessage McpRequest(string method, int id, object parameters, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id, method, @params = parameters })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task<JsonElement> ReadMcpJson(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            body = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..];
        }
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}