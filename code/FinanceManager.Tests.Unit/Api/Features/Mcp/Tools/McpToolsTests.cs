using FinanceManager.Api.Features.Mcp;
using FinanceManager.Api.Features.Mcp.Tools;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Portfolio.Valuation.Services;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Investments.Dtos;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using Microsoft.AspNetCore.Http;
using Moq;
using OpenIddict.Abstractions;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Unit.Api.Features.Mcp.Tools;

public sealed class McpToolsTests
{
    private const int _userId = 73;

    [Fact]
    public void UserContextAndIdentity_UseValidatedOAuthSubject()
    {
        var context = UserContext(
            new Claim(OpenIddictConstants.Claims.Subject, _userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, "999"),
            new Claim(OpenIddictConstants.Claims.Name, "mcp-user"),
            new Claim(OpenIddictConstants.Claims.Role, "User"));

        var identity = new IdentityTools(context).WhoAmI();

        Assert.Equal(_userId, context.GetUserId());
        Assert.Equal(_userId.ToString(), identity.UserId);
        Assert.Equal("mcp-user", identity.Login);
        Assert.Equal(["User"], identity.Roles);
    }

    [Fact]
    public void UserContext_RejectsMissingOrInvalidSubject()
    {
        Assert.Throws<InvalidOperationException>(() => UserContext().GetUserId());
        Assert.Throws<InvalidOperationException>(() =>
            UserContext(new Claim(OpenIddictConstants.Claims.Subject, "not-a-user-id")).GetUserId());
        Assert.Throws<InvalidOperationException>(() =>
            UserContext(new Claim(OpenIddictConstants.Claims.Subject, "0")).GetUserId());
    }

    [Fact]
    public void UserContext_AcceptsNegativeGuestIds()
    {
        Assert.Equal(-5, UserContext(new Claim(OpenIddictConstants.Claims.Subject, "-5")).GetUserId());
    }

    private readonly Mock<ITransactionLogService> _log = new();
    private readonly Mock<ICashTransactionReader> _cash = new();
    private readonly Mock<IBondTransactionReader> _bonds = new();
    private readonly Mock<IInvestmentTransactionRepository> _trades = new();

    private TransactionTools Tools(int userId = _userId) =>
        new(UserContext(new Claim(OpenIddictConstants.Claims.Subject, userId.ToString())), _log.Object, _cash.Object, _bonds.Object, _trades.Object);

    private static readonly DateTime _noon = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    private static TransactionLogItem Cash(long id, DateTime? at = null, string description = "Lunch", string? contractor = "private raw counterparty", string currency = "PLN", params string[] labels) =>
        new CurrencyTransactionLogItem(new CashLogEntry(id, at ?? _noon, -10m, description, 1, currency, contractor,
            [.. labels.Select((name, index) => new FinancialLabel { Id = index + 1, Name = name })]));

    private static TransactionLogItem Bond(long id, DateTime? at = null) =>
        new BondTransactionLogItem(new BondLogEntry(id, at ?? _noon, 3m, "bond note", 5, "PLN", "Treasury 2030", []));

    private static TransactionLogItem Trade(long id, DateTime? at = null) =>
        new InvestmentTransactionLogItem(Owned(_userId, id, at ?? _noon));

    private void SetupPages(params TransactionLogItemsPage[] pages)
    {
        var next = 0;
        _log.Setup(service => service.GetItemsPage(It.IsAny<int>(), It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => pages[Math.Min(next++, pages.Length - 1)]);
    }

    private static TransactionLogItemsPage Page(TransactionLogCursor? next, params TransactionLogItem[] items) => new(items, next);

    private static TransactionLogCursor Cursor(long id) => new(_noon, TransactionKind.Cash, id);

    [Fact]
    public async Task ListTransactions_MapsEveryKindWithoutAccounts_AndKeepsTheStreamOrder()
    {
        SetupPages(Page(null, Cash(3, description: "Lunch", labels: ["Food", "Daily"]), Bond(2), Trade(1)));

        var result = await Tools().ListTransactions(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["cash", "bond", "investment"], result.Transactions.Select(item => item.Kind));
        Assert.Equal([3L, 2, 1], result.Transactions.Select(item => item.TransactionId));
        Assert.Equal(["Daily", "Food"], result.Transactions[0].Labels);
        Assert.Equal("PLN", result.Transactions[0].Currency);
        Assert.Equal("Treasury 2030", result.Transactions[1].Description);
        Assert.Equal("Buy SAFE", result.Transactions[2].Description);
        Assert.Equal(2m, result.Transactions[2].Quantity);
        Assert.False(result.HasMore);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("account", json, StringComparison.OrdinalIgnoreCase);
        _log.Verify(service => service.GetItemsPage(_userId, It.Is<TransactionLogQuery>(query => query.PageSize == 100 && query.Kinds == null && query.Currencies == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTransactions_PassesKindCurrencyAndDatesToTheReaders_AndDefaultsToTheLast90UtcDays()
    {
        SetupPages(Page(null));

        await Tools().ListTransactions(new DateOnly(2026, 1, 2), new DateOnly(2026, 2, 3), kind: " Bond ", currency: "usd", cancellationToken: TestContext.Current.CancellationToken);
        await Tools().ListTransactions(cancellationToken: TestContext.Current.CancellationToken);

        _log.Verify(service => service.GetItemsPage(_userId, It.Is<TransactionLogQuery>(query =>
            query.Start == new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) && query.End.Date == new DateTime(2026, 2, 3) &&
            query.Kinds!.SequenceEqual(new[] { TransactionKind.Bond }) && query.Currencies!.SequenceEqual(new[] { "usd" })), It.IsAny<CancellationToken>()), Times.Once);
        _log.Verify(service => service.GetItemsPage(_userId, It.Is<TransactionLogQuery>(query =>
            (query.End.Date - query.Start.Date).TotalDays == 90 && query.End.Date == DateTime.UtcNow.Date), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTransactions_NeverSearchesOrMatchesThePrivateCounterpartyOrTradeNotes()
    {
        var trade = InvestmentTransaction(_userId, 10, 100, "SAFE", 2);
        trade.Description = "private trade note";
        SetupPages(Page(null, Cash(1, description: "Lunch", contractor: "secret-merchant"), new InvestmentTransactionLogItem(trade)));

        var byCounterparty = await Tools().ListTransactions(query: "secret-merchant", cancellationToken: TestContext.Current.CancellationToken);
        var byNote = await Tools().ListTransactions(query: "private trade note", cancellationToken: TestContext.Current.CancellationToken);
        var visible = await Tools().ListTransactions(query: "lunch", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(byCounterparty.Transactions);
        Assert.Empty(byNote.Transactions);
        Assert.Equal(1L, Assert.Single(visible.Transactions).TransactionId);
        // The search text is matched in the tool, on description and labels only; the readers' SQL search (which also covers the counterparty) never sees it.
        _log.Verify(service => service.GetItemsPage(It.IsAny<int>(), It.Is<TransactionLogQuery>(query => query.Search != null), It.IsAny<CancellationToken>()), Times.Never);
        var json = JsonSerializer.Serialize(visible, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("secret-merchant", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private trade note", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListTransactions_FiltersByLabel_AndPagesWithHasMore()
    {
        SetupPages(Page(null, Cash(5, labels: ["Food"]), Cash(4, labels: ["Rent"]), Cash(3, labels: ["food"]), Cash(2, labels: ["Food"])));

        var first = await Tools().ListTransactions(label: "FOOD", limit: 2, cancellationToken: TestContext.Current.CancellationToken);
        var second = await Tools().ListTransactions(label: "FOOD", offset: 2, limit: 2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([5L, 3], first.Transactions.Select(item => item.TransactionId));
        Assert.True(first.HasMore);
        Assert.Equal([2L], second.Transactions.Select(item => item.TransactionId));
        Assert.False(second.HasMore);
    }

    [Fact]
    public async Task ListTransactions_ReadsSeveralStreamPagesUntilThePageIsFull_WithStableSameDayOrder()
    {
        var sameDay = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        // Same posting time across kinds and ids: the order is entry id descending, then kind, exactly as the streams return it.
        SetupPages(
            Page(Cursor(9), Cash(9, sameDay), Cash(8, sameDay), Trade(8, sameDay)),
            Page(null, Bond(8, sameDay), Cash(7, sameDay)));

        var result = await Tools().ListTransactions(limit: 4, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([("cash", 9L), ("cash", 8), ("investment", 8), ("bond", 8)], result.Transactions.Select(item => (item.Kind, item.TransactionId)));
        Assert.True(result.HasMore);
        _log.Verify(service => service.GetItemsPage(_userId, It.Is<TransactionLogQuery>(query => query.Cursor == Cursor(9)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTransactions_HugeOffsetDoesNotOverflowAndReturnsAnEmptyPage()
    {
        SetupPages(Page(null, Cash(1)));

        var result = await Tools().ListTransactions(offset: int.MaxValue, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Transactions);
        Assert.False(result.HasMore);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task ListTransactions_RejectsInvalidPaging(int offset, int limit)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Tools().ListTransactions(offset: offset, limit: limit, cancellationToken: TestContext.Current.CancellationToken));
        _log.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListTransactions_RejectsInvalidKindsAndRanges()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Tools().ListTransactions(kind: "currency", cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => Tools().ListTransactions(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), cancellationToken: TestContext.Current.CancellationToken));
        _log.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListTransactions_ForAGuestUser_ReadsTheGuestsOwnRows()
    {
        SetupPages(Page(null, Cash(1)));

        await Tools(-9).ListTransactions(cancellationToken: TestContext.Current.CancellationToken);

        _log.Verify(service => service.GetItemsPage(-9, It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTransaction_ReadsOwnedRowsByKindAndId()
    {
        var cashRow = new CashLogEntry(4, _noon, -10m, "Lunch", 1, "PLN", "private raw counterparty", []);
        _cash.Setup(reader => reader.GetByIds(_userId, It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 4 })), It.IsAny<CancellationToken>())).ReturnsAsync([cashRow]);
        _bonds.Setup(reader => reader.GetById(_userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BondLogEntry(5, _noon, 3m, "bond note", 5, "PLN", "Treasury 2030", []));
        _trades.Setup(repository => repository.Get(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Owned(_userId, 6, _noon));

        var cash = await Tools().GetTransaction(" Cash ", 4, TestContext.Current.CancellationToken);
        var bond = await Tools().GetTransaction("bond", 5, TestContext.Current.CancellationToken);
        var trade = await Tools().GetTransaction("INVESTMENT", 6, TestContext.Current.CancellationToken);

        Assert.Equal(("cash", 4L, "Lunch"), (cash!.Kind, cash.TransactionId, cash.Description));
        Assert.Equal(("bond", 5L, "Treasury 2030"), (bond!.Kind, bond.TransactionId, bond.Description));
        Assert.Equal(("investment", 6L, "Buy SAFE"), (trade!.Kind, trade.TransactionId, trade.Description));
        Assert.DoesNotContain("private raw counterparty", JsonSerializer.Serialize(cash, new JsonSerializerOptions(JsonSerializerDefaults.Web)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTransaction_ReturnsNullForMissingForeignOrNonPositiveIds()
    {
        _cash.Setup(reader => reader.GetByIds(_userId, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bonds.Setup(reader => reader.GetById(_userId, It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BondLogEntry?)null);
        _trades.Setup(repository => repository.Get(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Owned(999, 7, _noon));

        Assert.Null(await Tools().GetTransaction("cash", 4, TestContext.Current.CancellationToken));
        Assert.Null(await Tools().GetTransaction("bond", 5, TestContext.Current.CancellationToken));
        Assert.Null(await Tools().GetTransaction("investment", 7, TestContext.Current.CancellationToken));
        Assert.Null(await Tools().GetTransaction("cash", 0, TestContext.Current.CancellationToken));
        Assert.Null(await Tools().GetTransaction("investment", 8, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetTransaction_RejectsAnInvalidKind()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Tools().GetTransaction("currency", 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvestmentPortfolio_PoolsTheSubjectsTradesPerListing()
    {
        var transactions = new Mock<IInvestmentTransactionRepository>();
        var valuation = new Mock<IInvestmentValuationService>();
        var currencies = new Mock<ICurrencyRepository>();
        var listings = new Mock<IAssetListingRepository>();
        currencies.Setup(repository => repository.GetByCode("PLN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.PLN);
        transactions.Setup(repository => repository.GetHoldingsByUserAsOf(_userId, new DateOnly(2026, 7, 21), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new InvestmentHolding(100, "OWN", "NYSE", AssetType.Stock, 3),
                new InvestmentHolding(200, "AAA", "NYSE", AssetType.Stock, 1)
            ]);
        valuation.Setup(service => service.GetValueByListingAsync(
                It.IsAny<IReadOnlyList<InvestmentHolding>>(), DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [100] = 1200m, [200] = 50m, [300] = 25m });
        listings.Setup(repository => repository.GetByIds(
                It.Is<IReadOnlyCollection<long>>(ids => ids.OrderBy(id => id).SequenceEqual(new long[] { 100, 200 })), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AssetListing(100, "OWN"), AssetListing(200, "AAA")]);
        var tools = new InvestmentTools(UserContext(Subject()), transactions.Object, valuation.Object, currencies.Object, listings.Object);

        var result = await tools.GetInvestmentPortfolio(
            new DateOnly(2026, 7, 21),
            cancellationToken: TestContext.Current.CancellationToken);

        // The total is the pooled valuation (listing 300 has no catalogue row but still counts), positions are sorted by ticker.
        Assert.Equal(1275m, result.TotalValue);
        Assert.Equal(["AAA", "OWN"], result.Positions.Select(position => position.Ticker));
        var own = result.Positions.Single(position => position.AssetListingId == 100);
        Assert.Equal(3, own.Quantity);
        Assert.Equal(1200m, own.Value);
        listings.Verify(repository => repository.Get(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvestmentPortfolio_ReturnsAnEmptyPortfolioWithoutHoldings()
    {
        var transactions = new Mock<IInvestmentTransactionRepository>();
        var valuation = new Mock<IInvestmentValuationService>();
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(repository => repository.GetByCode("PLN", It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.PLN);
        transactions.Setup(repository => repository.GetHoldingsByUserAsOf(_userId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var tools = new InvestmentTools(UserContext(Subject()), transactions.Object, valuation.Object, currencies.Object, new Mock<IAssetListingRepository>().Object);

        var result = await tools.GetInvestmentPortfolio(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0m, result.TotalValue);
        Assert.Empty(result.Positions);
        valuation.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReferenceData_RequiresIdentityAndMapsOnlySafeFields()
    {
        var currencies = new Mock<ICurrencyRepository>();
        var labels = new Mock<IFinancialLabelsRepository>();
        currencies.Setup(repository => repository.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(new[] { DefaultCurrency.USD }.ToAsyncEnumerable());
        labels.Setup(repository => repository.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[]
            {
                new FinancialLabel
                {
                    Id = 4,
                    Name = "Salary",
                    Classifications = [new FinancialLabelClassification { Id = 88, LabelId = 4, Kind = "income", Value = "salary" }]
                }
            }.ToAsyncEnumerable());
        var tools = new ReferenceDataTools(UserContext(Subject()), currencies.Object, labels.Object);

        var result = await tools.ListReferenceData(TestContext.Current.CancellationToken);

        Assert.Equal("USD", Assert.Single(result.Currencies).Code);
        var label = Assert.Single(result.Labels);
        Assert.Equal("Salary", label.Name);
        var classification = Assert.Single(label.Classifications);
        Assert.Equal("income", classification.Kind);
        Assert.Null(classification.GetType().GetProperty("Id"));
        Assert.Null(classification.GetType().GetProperty("LabelId"));
    }

    [Fact]
    public void ResponseMappings_OmitOwnershipAndInternalPersistenceFields()
    {
        var label = new FinancialLabel { Id = 4, Name = "Food" };
        var investment = InvestmentTransaction(_userId, 10, 100, "SAFE", 2);
        investment.Description = "private note";
        investment.CreatedAt = DateTimeOffset.UtcNow;
        investment.AssetListing.MarketDataSymbols = [new MarketDataSymbol { Symbol = "provider-secret" }];
        var payload = new object[]
        {
            McpResponseMapper.Transaction(new CashLogEntry(5, DateTime.UtcNow, -20, "Lunch", 1, "PLN", "private raw counterparty", [label])),
            McpResponseMapper.Transaction(new CashLogEntry(6, DateTime.UtcNow, -20, "", 1, "PLN", "fallback-sensitive counterparty", [])),
            McpResponseMapper.Transaction(investment),
            McpResponseMapper.InvestmentPosition(investment.AssetListing, 2, 40),
            McpResponseMapper.Currency(DefaultCurrency.USD),
            McpResponseMapper.Label(label)
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("Lunch", json, StringComparison.Ordinal);
        Assert.Contains("SAFE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accountId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contractorDetails", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private raw counterparty", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback-sensitive counterparty", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private note", json, StringComparison.Ordinal);
        Assert.DoesNotContain("createdAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider-secret", json, StringComparison.Ordinal);
    }

    private static McpUserContext UserContext(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "McpOAuth"))
        };
        return new McpUserContext(new HttpContextAccessor { HttpContext = httpContext });
    }

    private static Claim Subject() => new(OpenIddictConstants.Claims.Subject, _userId.ToString());

    private static AssetListing AssetListing(long id, string ticker) => new()
    {
        Id = id,
        Ticker = ticker,
        ExchangeMic = "XNYS",
        ExchangeName = "New York Stock Exchange",
        TradingCurrency = "USD"
    };

    private static InvestmentTransaction Owned(int userId, long id, DateTime at)
    {
        var transaction = InvestmentTransaction(userId, 10, 100, "SAFE", 2);
        transaction.EntryId = id;
        transaction.PostingDate = at;
        return transaction;
    }

    private static InvestmentTransaction InvestmentTransaction(int userId, int accountId, long listingId, string ticker, decimal quantity) => new()
    {
        EntryId = listingId,
        UserId = userId,
        AssetListingId = listingId,
        Type = InvestmentTransactionType.Buy,
        Quantity = quantity,
        UnitPrice = 100,
        Currency = "USD",
        TradeDate = new DateOnly(2026, 1, 1),
        AssetListing = AssetListing(listingId, ticker)
    };
}