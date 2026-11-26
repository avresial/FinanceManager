using FinanceManager.Api.Features.Identity.Controllers;
using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Infrastructure.Features.Mcp.OAuth;
using FinanceManager.Infrastructure.Persistence;
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
    private static readonly DateTime _postedAt = DateTime.UtcNow.Date;
    private readonly FinanceManagerApiTestApp _app;
    private readonly List<string?> _searches = [];

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
        // The owner readers answer like the database does: only the caller's rows, newest first (time, then id), with keyset boundaries.
        // The same posting time on purpose, so the order across kinds and ids is decided by the tie-breakers.
        var food = new FinancialLabel { Id = 51, Name = "Food" };
        List<CashLogEntry> cashRows =
        [
            new(null, 301, _postedAt, -20m, "MCP lunch", 1, "PLN", "internal counterparty", [food]),
            new(11, 302, _postedAt, -3m, "MCP coffee", 1, "PLN", "secret merchant", []),
            new(null, 303, _postedAt.AddDays(-1), -7m, "MCP dollars", 2, "USD", "other counterparty", [])
        ];
        List<BondLogEntry> bondRows = [new(null, 301, _postedAt, 3m, "private bond note", 7, "PLN", "Treasury 2030", [])];
        var cash = new Mock<ICashTransactionReader>();
        cash.Setup(reader => reader.GetHistory(_userId, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, OwnerHistoryQuery query, CancellationToken _) =>
            {
                _searches.Add(query.Search);
                return Page(cashRows, query, row => row.PostingDate, row => row.EntryId, row => row.Currency);
            });
        cash.Setup(reader => reader.GetByIds(_userId, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, IReadOnlyCollection<long> ids, CancellationToken _) => [.. cashRows.Where(row => ids.Contains(row.EntryId))]);
        var bonds = new Mock<IBondTransactionReader>();
        bonds.Setup(reader => reader.GetHistory(_userId, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, OwnerHistoryQuery query, CancellationToken _) =>
            {
                _searches.Add(query.Search);
                return Page(bondRows, query, row => row.PostingDate, row => row.EntryId, row => row.Currency);
            });
        bonds.Setup(reader => reader.GetById(_userId, 301, It.IsAny<CancellationToken>())).ReturnsAsync(bondRows[0]);
        var trade = InvestmentTransaction(_userId, 21, 301, "MCP", 2);
        trade.Description = "private trade note";
        var foreignTrade = InvestmentTransaction(999, 22, 402, "FOREIGN", 9);
        var investmentTransactions = new Mock<IInvestmentTransactionRepository>();
        investmentTransactions.Setup(repository => repository.GetHistoryByUser(_userId, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, OwnerHistoryQuery query, CancellationToken _) =>
            {
                _searches.Add(query.Search);
                return Page([trade], query, row => row.PostingDate, row => row.EntryId, row => row.Currency);
            });
        investmentTransactions.Setup(repository => repository.Get(301, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        investmentTransactions.Setup(repository => repository.Get(402, It.IsAny<CancellationToken>())).ReturnsAsync(foreignTrade);
        var valuation = new Mock<IInvestmentValuationService>();
        // Only the caller's pooled holdings are read: the user id comes from the MCP identity, never from tool input.
        valuation.Setup(service => service.GetValueByListingAsync(
                It.IsAny<IReadOnlyList<InvestmentHolding>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [401] = 1234m });
        investmentTransactions.Setup(repository => repository.GetHoldingsByUserAsOf(
                _userId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new InvestmentHolding(401, "MCP", "XLON", AssetType.ETF, 2)]);
        var assetListings = new Mock<IAssetListingRepository>();
        assetListings.Setup(repository => repository.GetByIds(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 401 })), It.IsAny<CancellationToken>()))
            .ReturnsAsync([InvestmentTransaction(_userId, 21, 401, "MCP", 2).AssetListing]);
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
            services.RemoveAll<ICashTransactionReader>();
            services.AddSingleton(cash.Object);
            services.RemoveAll<IBondTransactionReader>();
            services.AddSingleton(bonds.Object);
            services.RemoveAll<IInvestmentTransactionRepository>();
            services.AddSingleton(investmentTransactions.Object);
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
        HttpStatusCode responseStatus;
        using var expirationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, expirationTimeout.Token);
        do
        {
            using var request = McpRequest("tools/list", 1, new { }, accessToken);
            using var response = await client.SendAsync(request, linkedCancellation.Token);
            responseStatus = response.StatusCode;
            if (responseStatus == HttpStatusCode.Unauthorized)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), linkedCancellation.Token);
        } while (true);

        Assert.Equal(HttpStatusCode.Unauthorized, responseStatus);
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

        var advertisedTools = await ListTools(client, accessToken, cancellationToken);
        var toolNames = advertisedTools
            .Select(tool => tool.GetProperty("name").GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["get_investment_portfolio", "get_transaction", "list_reference_data", "list_transactions", "who_am_i"], toolNames.Order());
        Assert.DoesNotContain("list_financial_accounts", toolNames);
        Assert.DoesNotContain("get_financial_account", toolNames);
        Assert.All(advertisedTools, tool => Assert.True(
            tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean()));
        var portfolioTool = Assert.Single(advertisedTools, tool =>
            tool.GetProperty("name").GetString() == "get_investment_portfolio");
        Assert.True(portfolioTool.GetProperty("annotations").GetProperty("openWorldHint").GetBoolean());

        var transactions = await CallTool(client, accessToken, 4, "list_transactions", new
        {
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)
        }, cancellationToken);
        Assert.Contains("MCP lunch", transactions, StringComparison.Ordinal);
        Assert.Contains("Treasury 2030", transactions, StringComparison.Ordinal);
        Assert.Contains("Buy MCP", transactions, StringComparison.Ordinal);
        Assert.DoesNotContain("FOREIGN", transactions, StringComparison.Ordinal);

        var transaction = await CallTool(client, accessToken, 5, "get_transaction", new { kind = "cash", transactionId = 301 }, cancellationToken);
        Assert.Contains("MCP lunch", transaction, StringComparison.Ordinal);

        var foreignTransaction = await CallTool(client, accessToken, 6, "get_transaction", new { kind = "investment", transactionId = 402 }, cancellationToken);
        Assert.DoesNotContain("FOREIGN", foreignTransaction, StringComparison.OrdinalIgnoreCase);
        var missingTransaction = await CallTool(client, accessToken, 7, "get_transaction", new { kind = "cash", transactionId = 777 }, cancellationToken);
        Assert.DoesNotContain("\"transactionId\":777", missingTransaction, StringComparison.Ordinal);

        var portfolio = await CallTool(client, accessToken, 8, "get_investment_portfolio", new
        {
            asOfDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, cancellationToken);
        Assert.Contains("\"ticker\":\"MCP\"", portfolio, StringComparison.Ordinal);
        Assert.Contains("1234", portfolio, StringComparison.Ordinal);
        Assert.DoesNotContain("FOREIGN", portfolio, StringComparison.Ordinal);
        Assert.DoesNotContain("accountName", portfolio, StringComparison.OrdinalIgnoreCase);

        var referenceData = await CallTool(client, accessToken, 9, "list_reference_data", new { }, cancellationToken);
        Assert.Contains("PLN", referenceData, StringComparison.Ordinal);
        Assert.Contains("MCP category", referenceData, StringComparison.Ordinal);

        foreach (var response in new[] { transactions, transaction, foreignTransaction, missingTransaction, portfolio, referenceData })
        {
            Assert.DoesNotContain("userId", response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("createdAt", response, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("account", response, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ToolDiscovery_DescribesKindAndTransactionIdAndNoAccountParameters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var accessToken = await GetAccessToken(client, cancellationToken);

        var tools = await ListTools(client, accessToken, cancellationToken);

        var list = Assert.Single(tools, tool => tool.GetProperty("name").GetString() == "list_transactions");
        var listProperties = list.GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["currency", "endDate", "kind", "label", "limit", "offset", "query", "startDate"], listProperties.Order());
        var get = Assert.Single(tools, tool => tool.GetProperty("name").GetString() == "get_transaction");
        var getSchema = get.GetProperty("inputSchema");
        Assert.Equal(["kind", "transactionId"], getSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(["kind", "transactionId"], getSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()!).Order());
        Assert.DoesNotContain("account", list.GetProperty("description").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("account", get.GetProperty("description").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListTransactions_FiltersByKindAndCurrency_AndOrdersTiesByIdThenKind_AcrossStablePages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var accessToken = await GetAccessToken(client, cancellationToken);
        var range = new
        {
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)
        };

        var all = await ListedTransactions(client, accessToken, 10, new { range.startDate, range.endDate, limit = 100 }, cancellationToken);
        // Same posting time: id descending, then kind (cash, bond, investment); the older dollar row follows.
        Assert.Equal([("cash", 302L), ("cash", 301L), ("bond", 301L), ("investment", 301L), ("cash", 303L)],
            all.Transactions.Select(row => (row.Kind, row.Id)));

        var first = await ListedTransactions(client, accessToken, 11, new { range.startDate, range.endDate, limit = 2 }, cancellationToken);
        var second = await ListedTransactions(client, accessToken, 12, new { range.startDate, range.endDate, limit = 2, offset = 2 }, cancellationToken);
        var third = await ListedTransactions(client, accessToken, 13, new { range.startDate, range.endDate, limit = 2, offset = 4 }, cancellationToken);
        Assert.Equal(all.Transactions.Take(2), first.Transactions);
        Assert.Equal(all.Transactions.Skip(2).Take(2), second.Transactions);
        Assert.Equal(all.Transactions.Skip(4), third.Transactions);
        Assert.True(first.HasMore);
        Assert.True(second.HasMore);
        Assert.False(third.HasMore);

        var bondsOnly = await ListedTransactions(client, accessToken, 14, new { range.startDate, range.endDate, kind = "bond" }, cancellationToken);
        Assert.Equal([("bond", 301L)], bondsOnly.Transactions.Select(row => (row.Kind, row.Id)));
        var dollars = await ListedTransactions(client, accessToken, 15, new { range.startDate, range.endDate, currency = "usd" }, cancellationToken);
        Assert.Equal([("investment", 301L), ("cash", 303L)], dollars.Transactions.Select(row => (row.Kind, row.Id)));
        var labelled = await ListedTransactions(client, accessToken, 16, new { range.startDate, range.endDate, label = "FOOD" }, cancellationToken);
        Assert.Equal([("cash", 301L)], labelled.Transactions.Select(row => (row.Kind, row.Id)));
    }

    [Fact]
    public async Task ListTransactions_NeverSearchesOrExposesPrivateCounterpartyOrNotes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var accessToken = await GetAccessToken(client, cancellationToken);
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);

        var byCounterparty = await CallTool(client, accessToken, 20, "list_transactions", new { startDate, query = "secret merchant" }, cancellationToken);
        var byTradeNote = await CallTool(client, accessToken, 21, "list_transactions", new { startDate, query = "private trade note" }, cancellationToken);
        var byBondNote = await CallTool(client, accessToken, 22, "list_transactions", new { startDate, query = "private bond note" }, cancellationToken);
        var visible = await CallTool(client, accessToken, 23, "list_transactions", new { startDate, query = "coffee" }, cancellationToken);
        var detail = await CallTool(client, accessToken, 24, "get_transaction", new { kind = "cash", transactionId = 302 }, cancellationToken);
        var tradeDetail = await CallTool(client, accessToken, 25, "get_transaction", new { kind = "investment", transactionId = 301 }, cancellationToken);
        var bondDetail = await CallTool(client, accessToken, 26, "get_transaction", new { kind = "bond", transactionId = 301 }, cancellationToken);

        foreach (var empty in new[] { byCounterparty, byTradeNote, byBondNote })
            Assert.DoesNotContain("\"transactionId\"", empty, StringComparison.Ordinal);
        Assert.Contains("MCP coffee", visible, StringComparison.Ordinal);
        // The readers' SQL search also covers the counterparty, so the search text must never reach them.
        Assert.All(_searches, search => Assert.Null(search));
        foreach (var response in new[] { visible, detail, tradeDetail, bondDetail })
        {
            Assert.DoesNotContain("secret merchant", response, StringComparison.Ordinal);
            Assert.DoesNotContain("internal counterparty", response, StringComparison.Ordinal);
            Assert.DoesNotContain("private trade note", response, StringComparison.Ordinal);
            Assert.DoesNotContain("private bond note", response, StringComparison.Ordinal);
            Assert.DoesNotContain("contractor", response, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RetiredAccountTools_AreUnknownToTheServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateBrowser();
        var accessToken = await GetAccessToken(client, cancellationToken);

        using var request = McpRequest("tools/call", 30, new { name = "list_financial_accounts", arguments = new { } }, accessToken);
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-06-18");
        var response = await client.SendAsync(request, cancellationToken);
        var json = await ReadMcpJson(response, cancellationToken);

        var failed = json.TryGetProperty("error", out _) ||
            json.GetProperty("result").TryGetProperty("isError", out var isError) && isError.GetBoolean();
        Assert.True(failed, json.GetRawText());
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

    private static async Task<JsonElement[]> ListTools(HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        using var listRequest = McpRequest("tools/list", 1, new { }, accessToken);
        listRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-06-18");
        var listResponse = await client.SendAsync(listRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listJson = await ReadMcpJson(listResponse, cancellationToken);
        return [.. listJson.GetProperty("result").GetProperty("tools").EnumerateArray()];
    }

    private sealed record ListedRow(string Kind, long Id);

    private sealed record ListedPage(IReadOnlyList<ListedRow> Transactions, bool HasMore);

    private static async Task<ListedPage> ListedTransactions(
        HttpClient client, string accessToken, int id, object arguments, CancellationToken cancellationToken)
    {
        var result = JsonDocument.Parse(await CallTool(client, accessToken, id, "list_transactions", arguments, cancellationToken)).RootElement;
        var page = result.GetProperty("structuredContent");
        return new ListedPage(
            [.. page.GetProperty("transactions").EnumerateArray().Select(row => new ListedRow(row.GetProperty("kind").GetString()!, row.GetProperty("transactionId").GetInt64()))],
            page.GetProperty("hasMore").GetBoolean());
    }

    private static List<T> Page<T>(IEnumerable<T> rows, OwnerHistoryQuery query, Func<T, DateTime> date, Func<T, long> id, Func<T, string?> currency)
    {
        var page = rows.Where(row => (query.Start is not DateTime start || date(row) >= start) && (query.End is not DateTime end || date(row) <= end));
        if (query.Boundary is OwnerHistoryBoundary boundary)
            page = page.Where(row => date(row) < boundary.Date || date(row) == boundary.Date && (boundary.IncludeEqualId ? id(row) <= boundary.EntryId : id(row) < boundary.EntryId));
        if (query.NormalizedCurrencies() is { Count: > 0 } currencies)
            page = page.Where(row => currency(row) is string code && currencies.Contains(code.ToUpperInvariant()));
        return [.. page.OrderByDescending(date).ThenByDescending(id).Take(query.Take)];
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
            EntryId = listingId,
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