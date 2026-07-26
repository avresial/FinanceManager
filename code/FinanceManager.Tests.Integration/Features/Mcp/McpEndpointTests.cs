using FinanceManager.Api.Features.Identity.Controllers;
using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.OAuth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Mcp;

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
        var currencyAccounts = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        currencyAccounts.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([
            new CurrencyAccount(_userId, 11, "MCP cash", AccountLabel.Cash),
            new CurrencyAccount(999, 12, "Foreign cash", AccountLabel.Cash)
        ]);
        var bondAccounts = new Mock<IAccountRepository<BondAccount>>();
        bondAccounts.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([]);
        var investmentAccounts = new Mock<IAccountRepository<InvestmentAccount>>();
        investmentAccounts.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([
            new InvestmentAccount(_userId, 21, "MCP broker"),
            new InvestmentAccount(999, 22, "Foreign broker")
        ]);
        var currencyEntries = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        var currencyEntry = new CurrencyAccountEntry(11, 301, DateTime.UtcNow, 100, -20)
        {
            Description = "MCP lunch",
            ContractorDetails = "internal counterparty"
        };
        currencyEntries.Setup(repository => repository.GetRange(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([currencyEntry]);
        currencyEntries.Setup(repository => repository.Get(11, 301)).ReturnsAsync(currencyEntry);
        var bondEntries = new Mock<IBondAccountEntryRepository<BondAccountEntry>>();
        var investmentTransactions = new Mock<IInvestmentTransactionRepository>();
        investmentTransactions.Setup(repository => repository.GetByUser(
                _userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                InvestmentTransaction(_userId, 21, 401, "MCP", 2),
                InvestmentTransaction(999, 22, 402, "FOREIGN", 9)
            ]);
        var bondDetails = new Mock<IBondDetailsRepository>();
        var valuation = new Mock<IInvestmentValuationService>();
        valuation.Setup(service => service.GetAccountValueAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [21] = 1234m, [22] = 9999m });
        valuation.Setup(service => service.GetHoldingsAsOfAsync(
                21, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [401] = 2 });
        var assetListings = new Mock<IAssetListingRepository>();
        assetListings.Setup(repository => repository.Get(401, It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvestmentTransaction(_userId, 21, 401, "MCP", 2).AssetListing);
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(repository => repository.GetByCode("PLN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.PLN);
        currencies.Setup(repository => repository.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(new[] { DefaultCurrency.PLN }.ToAsyncEnumerable());
        var labels = new Mock<IFinancialLabelsRepository>();
        labels.Setup(repository => repository.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[] { new FinancialLabel { Id = 51, Name = "MCP category" } }.ToAsyncEnumerable());
        _app = new FinanceManagerApiTestApp(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddSingleton(users.Object);
            services.RemoveAll<ICurrencyAccountRepository<CurrencyAccount>>();
            services.AddSingleton(currencyAccounts.Object);
            services.RemoveAll<IAccountRepository<BondAccount>>();
            services.AddSingleton(bondAccounts.Object);
            services.RemoveAll<IAccountRepository<InvestmentAccount>>();
            services.AddSingleton(investmentAccounts.Object);
            services.RemoveAll<IAccountEntryRepository<CurrencyAccountEntry>>();
            services.AddSingleton(currencyEntries.Object);
            services.RemoveAll<IBondAccountEntryRepository<BondAccountEntry>>();
            services.AddSingleton(bondEntries.Object);
            services.RemoveAll<IInvestmentTransactionRepository>();
            services.AddSingleton(investmentTransactions.Object);
            services.RemoveAll<IBondDetailsRepository>();
            services.AddSingleton(bondDetails.Object);
            services.RemoveAll<IInvestmentValuationService>();
            services.AddSingleton(valuation.Object);
            services.RemoveAll<IAssetListingRepository>();
            services.AddSingleton(assetListings.Object);
            services.RemoveAll<ICurrencyRepository>();
            services.AddSingleton(currencies.Object);
            services.RemoveAll<IFinancialLabelsRepository>();
            services.AddSingleton(labels.Object);
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
    public async Task McpWithTokenMissingMcpScope_IsForbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var tokens = await GetTokenResponse(client, cancellationToken);
        var refreshToken = tokens.GetProperty("refresh_token").GetString();
        var refreshResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "finance-manager-mcp-test",
            ["refresh_token"] = refreshToken!,
            ["scope"] = "offline_access"
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.False(refreshed.TryGetProperty("scope", out var scope) &&
            scope.GetString()!.Split(' ').Contains("mcp", StringComparer.Ordinal));

        using var request = McpRequest(
            "tools/list", 1, new { }, refreshed.GetProperty("access_token").GetString());
        var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task McpWithRevokedAuthorization_IsUnauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var accessToken = await GetAccessToken(client, cancellationToken);
        using (var scope = _app.Services.CreateScope())
        {
            var authorizationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var revoked = 0;
            await foreach (var authorization in authorizationManager.FindBySubjectAsync(
                               _userId.ToString(), cancellationToken))
            {
                Assert.True(await authorizationManager.TryRevokeAsync(authorization, cancellationToken));
                revoked++;
            }
            Assert.True(revoked > 0);
        }

        using var request = McpRequest("tools/list", 1, new { }, accessToken);
        var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpWithRevokedAccessToken_IsUnauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var accessToken = await GetAccessToken(client, cancellationToken);
        using (var scope = _app.Services.CreateScope())
        {
            var tokenManager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var token = await tokenManager.FindByReferenceIdAsync(accessToken, cancellationToken);
            Assert.NotNull(token);
            Assert.True(await tokenManager.TryRevokeAsync(token, cancellationToken));
        }

        using var request = McpRequest("tools/list", 1, new { }, accessToken);
        var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpWithExpiredAccessToken_IsUnauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.GetUser(_userId)).ReturnsAsync(new User
        {
            UserId = _userId,
            Login = _userLogin,
            UserRole = UserRole.User,
            CreationDate = DateTime.UtcNow
        });
        using var app = new FinanceManagerApiTestApp(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddSingleton(users.Object);
        }, hostSettings: new Dictionary<string, string?>
        {
            ["McpOAuth:AccessTokenLifetime"] = "00:00:01"
        });
        using (var scope = app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<McpOAuthOptions>>().Value;
            await scope.ServiceProvider.GetRequiredService<McpOAuthConfigurationReconciler>()
                .ReconcileAsync(options, cancellationToken);
        }
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost/")
        });
        var accessToken = await GetAccessToken(client, cancellationToken, app);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        using var request = McpRequest("tools/list", 1, new { }, accessToken);
        var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
    public async Task AuthenticatedClient_CallsEveryReadOnlyToolGroupWithoutCrossUserLeakage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var accessToken = await GetAccessToken(client, cancellationToken);

        using var listRequest = McpRequest("tools/list", 1, new { }, accessToken);
        listRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-06-18");
        var listResponse = await client.SendAsync(listRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listJson = await ReadMcpJson(listResponse, cancellationToken);
        var advertisedTools = listJson.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        var toolNames = advertisedTools
            .Select(tool => tool.GetProperty("name").GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(new[]
        {
            "list_financial_accounts",
            "get_financial_account",
            "list_transactions",
            "get_transaction",
            "get_investment_portfolio",
            "list_reference_data"
        }.All(toolNames.Contains));
        Assert.All(advertisedTools, tool => Assert.True(
            tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean()));
        var portfolioTool = Assert.Single(advertisedTools, tool =>
            tool.GetProperty("name").GetString() == "get_investment_portfolio");
        Assert.True(portfolioTool.GetProperty("annotations").GetProperty("openWorldHint").GetBoolean());

        var accounts = await CallTool(client, accessToken, 2, "list_financial_accounts", new { }, cancellationToken);
        Assert.Contains("MCP cash", accounts, StringComparison.Ordinal);
        Assert.Contains("MCP broker", accounts, StringComparison.Ordinal);
        Assert.DoesNotContain("Foreign", accounts, StringComparison.OrdinalIgnoreCase);

        var account = await CallTool(client, accessToken, 3, "get_financial_account", new { accountId = 11 }, cancellationToken);
        Assert.Contains("MCP cash", account, StringComparison.Ordinal);

        var transactions = await CallTool(client, accessToken, 4, "list_transactions", new
        {
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)
        }, cancellationToken);
        Assert.Contains("MCP lunch", transactions, StringComparison.Ordinal);
        Assert.Contains("MCP broker", transactions, StringComparison.Ordinal);
        Assert.DoesNotContain("FOREIGN", transactions, StringComparison.Ordinal);
        Assert.DoesNotContain("internal counterparty", transactions, StringComparison.Ordinal);

        var transaction = await CallTool(client, accessToken, 5, "get_transaction", new
        {
            accountType = "currency",
            accountId = 11,
            transactionId = 301
        }, cancellationToken);
        Assert.Contains("MCP lunch", transaction, StringComparison.Ordinal);

        var foreignTransaction = await CallTool(client, accessToken, 6, "get_transaction", new
        {
            accountType = "currency",
            accountId = 12,
            transactionId = 777
        }, cancellationToken);
        Assert.DoesNotContain("Foreign", foreignTransaction, StringComparison.OrdinalIgnoreCase);

        var portfolio = await CallTool(client, accessToken, 7, "get_investment_portfolio", new
        {
            asOfDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, cancellationToken);
        Assert.Contains("MCP broker", portfolio, StringComparison.Ordinal);
        Assert.Contains("1234", portfolio, StringComparison.Ordinal);
        Assert.DoesNotContain("FOREIGN", portfolio, StringComparison.Ordinal);
        Assert.DoesNotContain("9999", portfolio, StringComparison.Ordinal);

        var referenceData = await CallTool(client, accessToken, 8, "list_reference_data", new { }, cancellationToken);
        Assert.Contains("PLN", referenceData, StringComparison.Ordinal);
        Assert.Contains("MCP category", referenceData, StringComparison.Ordinal);

        foreach (var response in new[] { accounts, account, transactions, transaction, foreignTransaction, portfolio, referenceData })
        {
            Assert.DoesNotContain("userId", response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("createdAt", response, StringComparison.OrdinalIgnoreCase);
        }
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

    private async Task<string> GetAccessToken(
        HttpClient client,
        CancellationToken cancellationToken,
        FinanceManagerApiTestApp? app = null) =>
        (await GetTokenResponse(client, cancellationToken, app)).GetProperty("access_token").GetString()!;

    private async Task<JsonElement> GetTokenResponse(
        HttpClient client,
        CancellationToken cancellationToken,
        FinanceManagerApiTestApp? app = null)
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
            ["token"] = IssueJwt(app),
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
        return await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private string IssueJwt(FinanceManagerApiTestApp? app = null)
    {
        using var scope = (app ?? _app).Services.CreateScope();
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

    private static async Task<string> CallTool(
        HttpClient client,
        string accessToken,
        int id,
        string name,
        object arguments,
        CancellationToken cancellationToken)
    {
        using var request = McpRequest("tools/call", id, new { name, arguments }, accessToken);
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-06-18");
        var response = await client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await ReadMcpJson(response, cancellationToken)).GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
        return result.GetRawText();
    }

    private static InvestmentTransaction InvestmentTransaction(
        int userId,
        int accountId,
        long listingId,
        string ticker,
        decimal quantity) => new()
        {
            Id = listingId,
            UserId = userId,
            AccountId = accountId,
            AssetListingId = listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = quantity,
            UnitPrice = 100,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AssetListing = new AssetListing
            {
                Id = listingId,
                Ticker = ticker,
                ExchangeMic = "XNYS",
                ExchangeName = "New York Stock Exchange",
                TradingCurrency = "USD"
            }
        };
}