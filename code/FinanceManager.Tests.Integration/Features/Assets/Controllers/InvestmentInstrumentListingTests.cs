using FinanceManager.Application.Assets.Discovery;
using FinanceManager.Application.Assets.Pricing;
using FinanceManager.Application.Assets.Resolution;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Assets.HttpClients;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Assets.Controllers;

[Trait("Category", "Integration")]
public class InvestmentInstrumentListingTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 91;
    private const long _listingId = 5001;
    private static readonly Mock<IInvestmentPriceProvider> _priceProvider = new();
    private static readonly Mock<IOpenFigiClient> _openFigiClient = new();
    private static readonly Mock<IAlphaVantageClient> _alphaVantageClient = new();
    private TestDatabase? _testDatabase;
    private static readonly Mock<IMarketDataSymbolResolver> _twelveDataResolver = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _priceProvider.Reset();
        _openFigiClient.Reset();
        _alphaVantageClient.Reset();
        _twelveDataResolver.Reset();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        var planVerifierMock = new Mock<IUserPlanVerifier>();
        planVerifierMock.Setup(x => x.CanAddMoreEntries(_testUserId, It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(planVerifierMock.Object);
        services.AddSingleton(_priceProvider.Object);
        services.AddSingleton(_openFigiClient.Object);
        services.AddSingleton(_alphaVantageClient.Object);
        services.RemoveAll<IMarketDataSymbolResolver>();
        var resolver = new Mock<IMarketDataSymbolResolver>();
        resolver.SetupGet(x => x.Provider).Returns(MarketDataProvider.AlphaVantage);
        resolver.SetupGet(x => x.Priority).Returns(100);
        resolver.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentDiscoveryResultDto instrument, CancellationToken _) =>
                string.IsNullOrWhiteSpace(instrument.ProviderSymbol) || string.IsNullOrWhiteSpace(instrument.TradingCurrency)
                    ? new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 100, "not found")
                    : new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 100, "candidate", instrument.ProviderSymbol, instrument.ExchangeCode, instrument.TradingCurrency));
        resolver.Setup(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>()))
            .Returns(async (MarketDataSymbolResolution candidate, CancellationToken ct) =>
                (await _alphaVantageClient.Object.GetDailySeries(candidate.Symbol!, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow,
                    new Currency { ShortName = candidate.Currency! }, ct)).Any(x => x.PricePerUnit > 0));
        services.AddSingleton(resolver.Object);
        _twelveDataResolver.SetupGet(x => x.Provider).Returns(MarketDataProvider.TwelveData);
        _twelveDataResolver.SetupGet(x => x.Priority).Returns(200);
        _twelveDataResolver.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketDataSymbolResolution(MarketDataProvider.TwelveData, 200, "not found"));
        services.AddSingleton(_twelveDataResolver.Object);
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
        await SeedListing();
        var transaction = new InvestmentTransaction
        {
            UserId = _testUserId,
            AssetListingId = _listingId,
            Type = type,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = "USD",
            TradeDate = tradeDate
        };
        _testDatabase!.Context.InvestmentTransactions.Add(transaction);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return transaction.EntryId;
    }

    [Fact]
    public async Task ListingLookups_ReturnTheSeededInstrument()
    {
        await SeedListing();
        Authorize("owner", _testUserId, UserRole.User);
        var client = new InstrumentSearchHttpClient(Client);
        Assert.Equal(_listingId, Assert.Single(await client.SearchListingsAsync("CSPX", cancellationToken: TestContext.Current.CancellationToken)).ListingId);
        Assert.Equal(_listingId, (await client.GetListingAsync(_listingId))!.ListingId);
    }

    [Fact]
    public async Task ListingPrice_UsesPriceProvider()
    {
        await SeedListing();
        Authorize("testuser", _testUserId, UserRole.User);
        _priceProvider
            .Setup(x => x.GetPricePerUnitResultAsync(_listingId, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvestmentPriceResult.Success(321.45m));

        var result = await new InstrumentSearchHttpClient(Client).GetListingPriceAsync(_listingId);

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
            .Setup(x => x.GetPricePerUnitResultAsync(_listingId, It.IsAny<Currency>(),
                tradeDate.ToDateTime(TimeOnly.MinValue), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvestmentPriceResult.Success(300m));

        var result = await new InstrumentSearchHttpClient(Client).GetListingPriceAsync(_listingId, tradeDate);

        Assert.NotNull(result);
        Assert.Equal(300m, result.LatestPrice);
    }

    [Fact]
    public async Task ListingPrice_WhenPriceIsPending_ReturnsSafeRetryMessage()
    {
        await SeedListing();
        Authorize("testuser", _testUserId, UserRole.User);
        var retryAtUtc = DateTime.UtcNow.Date.AddDays(1);
        var pending = new InvestmentPriceResult.NotYetPublished(
            "The exchange rate for today is not published yet.",
            retryAtUtc);
        _priceProvider
            .Setup(x => x.GetPricePerUnitResultAsync(
                _listingId,
                It.IsAny<Currency>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);

        var result = await new InstrumentSearchHttpClient(Client).GetListingPriceAsync(_listingId);

        Assert.NotNull(result);
        Assert.Null(result.LatestPrice);
        Assert.Equal(pending.Message, result.Message);
        Assert.Equal(retryAtUtc, result.RetryAtUtc);
    }

    [Fact]
    public async Task Add_WithExternalInstrument_CommitsInstrumentAndTransactionTogether()
    {
        SetupExternalInstrument();
        Authorize("testuser", _testUserId, UserRole.User);

        var options = await Client.GetFromJsonAsync<List<InvestmentInstrumentOptionDto>>(
            "api/investments/instruments/options?q=CSPX",
            TestContext.Current.CancellationToken);
        var option = Assert.Single(options!);
        Assert.Equal(InstrumentOptionSource.External, option.Source);

        var request = new CreateInvestmentTradeRequest(null,
            InvestmentTransactionTypeDto.Buy,
            2m,
            100m,
            "USD",
            new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: option.ResultId);
        var response = await Client.PostAsJsonAsync(
            "api/transactions/investment", request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var transaction = await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(transaction);
        Assert.True(transaction!.AssetListingId > 0);
        Assert.True(transaction.AutomaticPricesAvailable);
        Assert.Single(await _testDatabase!.Context.Assets.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _testDatabase.Context.AssetListings.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _testDatabase.Context.MarketDataSymbols.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _testDatabase.Context.InvestmentTransactions.ToListAsync(TestContext.Current.CancellationToken));
        // The provider symbol is validated once, before the transaction, and never re-validated inside it.
        _alphaVantageClient.Verify(
            x => x.GetDailySeries(
                "CSPX.LON",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<Currency>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Add_WithMultipleConfiguredProviders_PersistsSymbolsAndPreservesDisabledMappingOnReimport()
    {
        SetupExternalInstrument();
        _twelveDataResolver.SetupGet(x => x.Priority).Returns(50);
        _twelveDataResolver.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketDataSymbolResolution(MarketDataProvider.TwelveData, 50, "candidate", "CSPX:LSE", "LSE", "USD"));
        _twelveDataResolver.Setup(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Authorize("testuser", _testUserId, UserRole.User);
        var options = await Client.GetFromJsonAsync<List<InvestmentInstrumentOptionDto>>(
            "api/investments/instruments/options?q=CSPX", TestContext.Current.CancellationToken);
        var request = new CreateInvestmentTradeRequest(null, InvestmentTransactionTypeDto.Buy,
            2m, 100m, "USD", new DateOnly(2026, 1, 1), ExternalInstrumentResultId: Assert.Single(options!).ResultId);
        var response = await Client.PostAsJsonAsync("api/transactions/investment", request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var symbols = await _testDatabase!.Context.MarketDataSymbols.OrderBy(x => x.Provider).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, symbols.Count);
        Assert.False(symbols[0].IsPrimary);
        Assert.True(symbols[1].IsPrimary);
        Assert.Equal("CSPX:LSE", symbols[1].Symbol);
        Assert.Equal("LSE", symbols[1].ProviderExchangeCode);
        Assert.Equal("USD", symbols[1].Currency);
        Assert.All(symbols, x => { Assert.True(x.IsEnabled); Assert.NotNull(x.LastValidatedAt); Assert.Null(x.LastError); });
        Assert.Single(await _testDatabase.Context.InvestmentTransactions.ToListAsync(TestContext.Current.CancellationToken));

        symbols[0].IsEnabled = false;
        symbols[0].LastValidatedAt = null;
        symbols[0].LastError = "Previous provider validation failed";
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _testDatabase.Context.ChangeTracker.Clear();

        var reimport = await Client.PostAsJsonAsync("api/transactions/investment", request, TestContext.Current.CancellationToken);
        reimport.EnsureSuccessStatusCode();
        _testDatabase.Context.ChangeTracker.Clear();
        var retriedSymbol = await _testDatabase.Context.MarketDataSymbols.AsNoTracking()
            .SingleAsync(x => x.Provider == MarketDataProvider.AlphaVantage, TestContext.Current.CancellationToken);
        Assert.False(retriedSymbol.IsEnabled);
        Assert.False(retriedSymbol.IsPrimary);
        Assert.NotNull(retriedSymbol.LastValidatedAt);
        Assert.Null(retriedSymbol.LastError);
        Assert.Equal(2, await _testDatabase.Context.MarketDataSymbols.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Add_WhenAllPriceProvidersFail_SavesCatalogInstrumentWithoutSymbolsAndSavesTransaction()
    {
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
            "api/investments/instruments/options?q=CSPX",
            TestContext.Current.CancellationToken);
        var request = new CreateInvestmentTradeRequest(null,
            InvestmentTransactionTypeDto.Buy,
            2m,
            100m,
            "USD",
            new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: Assert.Single(options!).ResultId);
        var response = await Client.PostAsJsonAsync(
            "api/transactions/investment", request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(saved);
        Assert.Equal(100m, saved!.UnitPrice);
        Assert.False(saved.AutomaticPricesAvailable);
        Assert.Single(await _testDatabase!.Context.Assets.ToListAsync(TestContext.Current.CancellationToken));
        var listing = Assert.Single(await _testDatabase.Context.AssetListings.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal("CSPX", listing.Ticker);
        Assert.Equal("XLON", listing.ExchangeMic);
        Assert.Equal("USD", listing.TradingCurrency);
        Assert.True(listing.IsIncomplete);
        Assert.Empty(await _testDatabase.Context.MarketDataSymbols.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _testDatabase.Context.InvestmentTransactions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Add_WithIncompleteExternalInstrumentMissingVenueAndCurrency_SavesCatalogListingAndSupportsReadEndpoints()
    {
        _openFigiClient
            .Setup(x => x.MapByTickerAsync("VWCE", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new OpenFigiListing(
                    Isin: null,
                    Ticker: "VWCE",
                    Name: "VANG FTSE AW USDA",
                    ExchCode: "GF",
                    Figi: "BBG000GF001",
                    ShareClassFigi: "BBG000SC001",
                    SecurityType: null)
            ]);
        _alphaVantageClient
            .Setup(x => x.SearchTicker("VWCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Authorize("testuser", _testUserId, UserRole.User);

        var options = await Client.GetFromJsonAsync<List<InvestmentInstrumentOptionDto>>(
            "api/investments/instruments/options?q=VWCE",
            TestContext.Current.CancellationToken);
        var option = Assert.Single(options!);
        Assert.Equal(InstrumentOptionSource.External, option.Source);
        Assert.Null(option.ExchangeMic);
        Assert.Equal(string.Empty, option.TradingCurrency);
        Assert.NotEmpty(option.Warnings);

        var request = new CreateInvestmentTradeRequest(null,
            InvestmentTransactionTypeDto.Buy,
            4m,
            117.50m,
            "EUR",
            new DateOnly(2026, 2, 10),
            ExternalInstrumentResultId: option.ResultId);
        var response = await Client.PostAsJsonAsync(
            "api/transactions/investment", request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(created);
        Assert.False(created!.AutomaticPricesAvailable);
        Assert.Equal("EUR", created.Currency);
        Assert.Equal(117.50m, created.UnitPrice);

        var asset = Assert.Single(await _testDatabase!.Context.Assets.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(AssetType.Other, asset.Type);
        var listing = Assert.Single(await _testDatabase.Context.AssetListings.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Null(listing.ExchangeMic);
        Assert.Null(listing.TradingCurrency);
        Assert.Equal("GF", listing.ExchangeName);
        Assert.True(listing.IsIncomplete);
        Assert.Empty(await _testDatabase.Context.MarketDataSymbols.ToListAsync(TestContext.Current.CancellationToken));

        // Subsequent search surfaces the saved catalog listing as Existing with gap warnings
        var repeatOptions = await Client.GetFromJsonAsync<List<InvestmentInstrumentOptionDto>>(
            "api/investments/instruments/options?q=VWCE",
            TestContext.Current.CancellationToken);
        var existingOption = Assert.Single(repeatOptions!);
        Assert.Equal(InstrumentOptionSource.Existing, existingOption.Source);
        Assert.Equal(listing.Id, existingOption.AssetListingId);

        // ListingPrice endpoint safely returns null LatestPrice when TradingCurrency is unknown
        var listingPrice = await new InstrumentSearchHttpClient(Client).GetListingPriceAsync(listing.Id);
        Assert.NotNull(listingPrice);
        Assert.Null(listingPrice!.LatestPrice);
        Assert.Equal(string.Empty, listingPrice.Currency);

        // History page endpoint reads back the saved transaction without errors
        var history = await Client.GetFromJsonAsync<InvestmentTransactionHistoryPageDto>(
            "api/transactions/investment",
            TestContext.Current.CancellationToken);
        Assert.Single(history!.Items);
        Assert.Equal("EUR", history.Items[0].Currency);
    }

    [Fact]
    public async Task Add_WithUnrecognizedExternalResultId_ReturnsBadRequest()
    {
        Authorize("testuser", _testUserId, UserRole.User);

        var request = new CreateInvestmentTradeRequest(null,
            InvestmentTransactionTypeDto.Buy,
            1m,
            100m,
            "USD",
            new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: "unrecognized-result-id");
        var context = _testDatabase!.Context;
        var ct = TestContext.Current.CancellationToken;
        var assetsBefore = await context.Assets.CountAsync(ct);
        var listingsBefore = await context.AssetListings.CountAsync(ct);
        var symbolsBefore = await context.MarketDataSymbols.CountAsync(ct);
        var transactionsBefore = await context.InvestmentTransactions.CountAsync(ct);

        var response = await Client.PostAsJsonAsync("api/transactions/investment", request, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Rejection happens before anything is written to the shared catalog.
        Assert.Equal(assetsBefore, await context.Assets.CountAsync(ct));
        Assert.Equal(listingsBefore, await context.AssetListings.CountAsync(ct));
        Assert.Equal(symbolsBefore, await context.MarketDataSymbols.CountAsync(ct));
        Assert.Equal(transactionsBefore, await context.InvestmentTransactions.CountAsync(ct));
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
                    Figi: "BBG001",
                    ShareClassFigi: "BBGSC",
                    SecurityType: "ETP")
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