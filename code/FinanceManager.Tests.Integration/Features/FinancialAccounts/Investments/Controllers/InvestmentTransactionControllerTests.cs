using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Investments.Controllers;

[Trait("Category", "Integration")]
public class InvestmentTransactionControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 91;
    private const int _testAccountId = 791;
    private const long _listingId = 5001;
    private static readonly Mock<IInvestmentPriceProvider> _priceProvider = new();
    private static readonly Mock<IOpenFigiClient> _openFigiClient = new();
    private static readonly Mock<IAlphaVantageClient> _alphaVantageClient = new();
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _priceProvider.Reset();
        _openFigiClient.Reset();
        _alphaVantageClient.Reset();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        var planVerifierMock = new Mock<IUserPlanVerifier>();
        planVerifierMock.Setup(x => x.CanAddMoreAccounts(_testUserId)).ReturnsAsync(true);
        planVerifierMock.Setup(x => x.CanAddMoreEntries(_testUserId, It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(planVerifierMock.Object);
        services.AddSingleton(_priceProvider.Object);
        services.AddSingleton(_openFigiClient.Object);
        services.AddSingleton(_alphaVantageClient.Object);
    }

    private async Task SeedAccount()
    {
        if (_testDatabase is null) return;
        if (await _testDatabase.Context.Accounts.AnyAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken)) return;
        _testDatabase.Context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = _testAccountId,
            UserId = _testUserId,
            Name = "Test Investment Account",
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A transaction always references a real listing; seed one so the read path's Include(AssetListing)
    // (a required relationship) returns the rows rather than filtering them out.
    private async Task SeedListing()
    {
        if (_testDatabase is null) return;
        if (!await _testDatabase.Context.Assets.AnyAsync(a => a.Id == 1, TestContext.Current.CancellationToken))
        {
            _testDatabase.Context.Assets.Add(new Asset
            {
                Id = 1,
                Name = "iShares Core S&P 500 UCITS ETF",
                Type = AssetType.ETF
            });
            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        if (await _testDatabase.Context.AssetListings.AnyAsync(l => l.Id == _listingId, TestContext.Current.CancellationToken)) return;
        _testDatabase.Context.AssetListings.Add(new AssetListing
        {
            Id = _listingId,
            AssetId = 1,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD",
            PriceMultiplier = 1m,
            IsActive = true
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<long> SeedTransaction(InvestmentTransactionType type, decimal quantity, DateOnly tradeDate, decimal unitPrice = 100m)
    {
        await SeedAccount();
        await SeedListing();
        var transaction = new InvestmentTransaction
        {
            UserId = _testUserId,
            AccountId = _testAccountId,
            AssetListingId = _listingId,
            Type = type,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = "USD",
            TradeDate = tradeDate
        };
        _testDatabase!.Context.InvestmentTransactions.Add(transaction);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return transaction.Id;
    }

    private static AddInvestmentTransactionRequest AddRequest(int accountId) => new(
        accountId, _listingId, InvestmentTransactionType.Buy, 3m, 120m, "USD", new DateOnly(2024, 6, 1));

    [Fact]
    public async Task ListingPrice_UsesPriceProvider()
    {
        await SeedListing();
        Authorize("testuser", _testUserId, UserRole.User);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, It.IsAny<FinanceManager.Domain.FinancialAccounts.Currencies.Entities.Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(321.45m);

        var result = await new InvestmentTransactionHttpClient(Client).GetListingPriceAsync(_listingId);

        Assert.NotNull(result);
        Assert.Equal(321.45m, result.LatestPrice);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public async Task ListingPrice_UsesTradeDateWhenProvided()
    {
        await SeedListing();
        Authorize("testuser", _testUserId, UserRole.User);
        var tradeDate = new DateOnly(2024, 6, 1);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, It.IsAny<FinanceManager.Domain.FinancialAccounts.Currencies.Entities.Currency>(),
                tradeDate.ToDateTime(TimeOnly.MinValue), It.IsAny<CancellationToken>()))
            .ReturnsAsync(300m);

        var result = await new InvestmentTransactionHttpClient(Client).GetListingPriceAsync(_listingId, tradeDate);

        Assert.NotNull(result);
        Assert.Equal(300m, result.LatestPrice);
    }

    [Fact]
    public async Task Add_CreatesTransaction()
    {
        await SeedAccount();
        await SeedListing();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var result = await client.AddAsync(AddRequest(_testAccountId));

        Assert.NotNull(result);
        Assert.Equal(_testUserId, result!.UserId);
        Assert.Equal(3m, result.Quantity);

        var inDb = await _testDatabase!.Context.InvestmentTransactions
            .FirstOrDefaultAsync(x => x.AccountId == _testAccountId, TestContext.Current.CancellationToken);
        Assert.NotNull(inDb);
        Assert.Equal(120m, inDb!.UnitPrice);
        Assert.Equal(_testUserId, inDb.UserId);
    }

    [Fact]
    public async Task Add_WithExternalInstrument_CommitsInstrumentAndTransactionTogether()
    {
        await SeedAccount();
        SetupExternalInstrument();
        Authorize("testuser", _testUserId, UserRole.User);

        var options = await Client.GetFromJsonAsync<List<InvestmentInstrumentOptionDto>>(
            "api/InvestmentTransaction/SearchInstruments?q=CSPX",
            TestContext.Current.CancellationToken);
        var option = Assert.Single(options!);
        Assert.Equal(InstrumentOptionSource.External, option.Source);

        var request = new AddInvestmentTransactionRequest(
            _testAccountId,
            null,
            InvestmentTransactionType.Buy,
            2m,
            100m,
            "USD",
            new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: option.ResultId);
        var response = await Client.PostAsJsonAsync(
            "api/InvestmentTransaction/Add", request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var transaction = await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(transaction);
        Assert.True(transaction!.AssetListingId > 0);
        Assert.Single(await _testDatabase!.Context.Assets.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _testDatabase.Context.AssetListings.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _testDatabase.Context.MarketDataSymbols.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _testDatabase.Context.InvestmentTransactions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Add_WithInvalidExternalProviderResult_CreatesNoMasterData()
    {
        await SeedAccount();
        SetupExternalInstrument();
        _alphaVantageClient
            .Setup(x => x.GetDailySeries(
                "CSPX.LON",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<Currency>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Authorize("testuser", _testUserId, UserRole.User);

        var options = await Client.GetFromJsonAsync<List<InvestmentInstrumentOptionDto>>(
            "api/InvestmentTransaction/SearchInstruments?q=CSPX",
            TestContext.Current.CancellationToken);
        var request = new AddInvestmentTransactionRequest(
            _testAccountId,
            null,
            InvestmentTransactionType.Buy,
            2m,
            100m,
            "USD",
            new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: Assert.Single(options!).ResultId);
        var response = await Client.PostAsJsonAsync(
            "api/InvestmentTransaction/Add", request, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("market_data_symbol_invalid", problem!["code"]?.ToString());
        Assert.Empty(await _testDatabase!.Context.Assets.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _testDatabase.Context.AssetListings.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _testDatabase.Context.MarketDataSymbols.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _testDatabase.Context.InvestmentTransactions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Add_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedAccount();
        Authorize("otheruser", _testUserId + 1, UserRole.User);

        var response = await Client.PostAsJsonAsync("api/InvestmentTransaction/Add", AddRequest(_testAccountId), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Add_WithInvalidQuantity_ReturnsBadRequest()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var invalid = AddRequest(_testAccountId) with { Quantity = 0m };

        var response = await Client.PostAsJsonAsync("api/InvestmentTransaction/Add", invalid, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetByAccount_ReturnsTransactions()
    {
        await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        await SeedTransaction(InvestmentTransactionType.Sell, 2m, new DateOnly(2024, 2, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var transactions = await client.GetByAccountAsync(_testAccountId);

        Assert.Equal(2, transactions.Count);
    }

    [Fact]
    public async Task GetByAccount_ReturnsAssetType()
    {
        await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var transaction = Assert.Single(await client.GetByAccountAsync(_testAccountId));

        Assert.Equal(AssetType.ETF, transaction.AssetType);
    }

    [Fact]
    public async Task GetByAccount_RecoversAllMissingPrices()
    {
        var tradeDate = new DateOnly(2024, 1, 10);
        await SeedTransaction(InvestmentTransactionType.Buy, 5m, tradeDate, unitPrice: 0m);
        await SeedTransaction(InvestmentTransactionType.Sell, 2m, tradeDate, unitPrice: 0m);
        Authorize("testuser", _testUserId, UserRole.User);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, It.IsAny<FinanceManager.Domain.FinancialAccounts.Currencies.Entities.Currency>(), It.Is<DateTime>(d => d.Date == tradeDate.ToDateTime(TimeOnly.MinValue).Date), It.IsAny<CancellationToken>()))
            .ReturnsAsync(321.45m);

        var transactions = await new InvestmentTransactionHttpClient(Client).GetByAccountAsync(_testAccountId);

        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, transaction => Assert.Equal(321.45m, transaction.UnitPrice));
        Assert.All(await _testDatabase!.Context.InvestmentTransactions.ToListAsync(TestContext.Current.CancellationToken), transaction => Assert.Equal(321.45m, transaction.UnitPrice));
        _priceProvider.Verify(
            x => x.GetPricePerUnitAsync(_listingId, It.IsAny<FinanceManager.Domain.FinancialAccounts.Currencies.Entities.Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Get_ReturnsTransaction()
    {
        var id = await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var result = await client.GetAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
        Assert.Equal(_testAccountId, result.AccountId);
    }

    [Fact]
    public async Task Update_ForUnknownTransaction_ReturnsFalse()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);
        var update = new UpdateInvestmentTransactionRequest(
            long.MaxValue, _testAccountId, _listingId, InvestmentTransactionType.Buy, 8m, 130m, "USD", new DateOnly(2024, 1, 10));

        var result = await client.UpdateAsync(update);

        Assert.False(result);
    }

    [Fact]
    public async Task Delete_ForUnknownTransaction_ReturnsFalse()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var result = await client.DeleteAsync(_testAccountId, long.MaxValue);

        Assert.False(result);
    }

    [Fact]
    public async Task Update_ModifiesTransaction()
    {
        var id = await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);
        var update = new UpdateInvestmentTransactionRequest(
            id, _testAccountId, _listingId, InvestmentTransactionType.Buy, 8m, 130m, "USD", new DateOnly(2024, 1, 10));

        var result = await client.UpdateAsync(update);

        Assert.True(result);
        var inDb = await _testDatabase!.Context.InvestmentTransactions
            .FirstOrDefaultAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(8m, inDb!.Quantity);
        Assert.Equal(130m, inDb.UnitPrice);
    }

    [Fact]
    public async Task Delete_RemovesTransaction()
    {
        var id = await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var result = await client.DeleteAsync(_testAccountId, id);

        Assert.True(result);
        var inDb = await _testDatabase!.Context.InvestmentTransactions
            .FirstOrDefaultAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        Assert.Null(inDb);
    }

    private void SetupExternalInstrument()
    {
        _openFigiClient
            .Setup(x => x.MapByTickerAsync("CSPX", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new OpenFigiListing(
                    Isin: null,
                    Ticker: "CSPX",
                    Name: "iShares Core S&P 500 UCITS ETF",
                    ExchCode: "LN",
                    Currency: "USD",
                    Figi: "BBG001",
                    ShareClassFigi: "BBGSC")
            ]);
        _alphaVantageClient
            .Setup(x => x.SearchTicker("CSPX", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TickerSearchMatch
                {
                    Symbol = "CSPX.LON",
                    Name = "iShares Core S&P 500 ETF",
                    Type = "ETF",
                    Region = "United Kingdom",
                    Currency = "USD",
                    MatchScore = 1m
                }
            ]);
        _alphaVantageClient
            .Setup(x => x.GetDailySeries(
                "CSPX.LON",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<Currency>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StockPrice
                {
                    Isin = "CSPX.LON",
                    PricePerUnit = 100m,
                    Currency = new Currency(0, "USD", "USD"),
                    Date = DateTime.UtcNow
                }
            ]);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}