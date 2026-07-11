using FinanceManager.Application.Assets.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Assets;

[Trait("Category", "Unit")]
public class InvestmentPriceProviderTests
{
    private static readonly Currency _usd = new(1, "USD", "$");

    private readonly Mock<IAssetListingRepository> _listingRepository = new();
    private readonly Mock<IMarketDataSymbolRepository> _symbolRepository = new();
    private readonly InMemoryPriceQuoteRepository _priceQuoteRepository = new();
    private readonly Mock<IStockPriceSource> _priceSource = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public InvestmentPriceProviderTests()
    {
        _symbolRepository
            .Setup(x => x.RecordFetchResult(It.IsAny<long>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // GetOrAdd echoes back a currency with the requested short name.
        _currencyRepository
            .Setup(x => x.GetOrAdd(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string shortName, string? symbol, CancellationToken _) => new Currency(0, shortName, symbol ?? shortName));
    }

    private InvestmentPriceProvider CreateSut() => new(
        _listingRepository.Object,
        _symbolRepository.Object,
        _priceQuoteRepository,
        _priceSource.Object,
        _currencyRepository.Object,
        _currencyExchangeService.Object,
        _cache,
        LoggerFactory.Create(_ => { }).CreateLogger<InvestmentPriceProvider>());

    private static AssetListing Listing(string tradingCurrency = "USD", decimal multiplier = 1m) => new()
    {
        Id = 10,
        AssetId = 1,
        Ticker = "CSPX",
        ExchangeMic = "XLON",
        ExchangeName = "London Stock Exchange",
        TradingCurrency = tradingCurrency,
        PriceMultiplier = multiplier,
        IsActive = true,
        MarketDataSymbols =
        [
            new MarketDataSymbol
            {
                Id = 100,
                AssetListingId = 10,
                Provider = MarketDataProvider.AlphaVantage,
                Symbol = "CSPX.LON",
                IsPrimary = true,
                IsEnabled = true
            }
        ]
    };

    private static StockPrice Price(decimal value, DateTime date) =>
        new() { Isin = string.Empty, PricePerUnit = value, Currency = _usd, Date = date };

    [Fact]
    public async Task GetPricePerUnit_ServesCachedQuote_WithoutFetching()
    {
        var asOf = new DateTime(2024, 6, 3);
        _listingRepository.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(Listing());
        _priceQuoteRepository.Seed(new PriceQuote
        {
            AssetListingId = 10,
            Provider = MarketDataProvider.AlphaVantage,
            Price = 123m,
            Currency = "USD",
            PriceTime = new DateTimeOffset(asOf, TimeSpan.Zero),
            QuoteType = PriceQuoteType.EndOfDay
        });

        var result = await CreateSut().GetPricePerUnitAsync(10, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(123m, result);
        _priceSource.Verify(
            x => x.GetDailySeries(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPricePerUnit_FetchesPersistsAndReturns_WhenNoQuoteCached()
    {
        var asOf = new DateTime(2024, 6, 3);
        _listingRepository.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(Listing());
        _priceSource
            .Setup(x => x.GetDailySeries("CSPX.LON", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Price(200m, asOf)]);

        var result = await CreateSut().GetPricePerUnitAsync(10, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(200m, result);
        var stored = Assert.Single(_priceQuoteRepository.All);
        Assert.Equal(200m, stored.Price);
        Assert.Equal("USD", stored.Currency);
        Assert.Equal(MarketDataProvider.AlphaVantage, stored.Provider);
        Assert.Equal(100, stored.MarketDataSymbolId);
    }

    [Fact]
    public async Task GetPricePerUnit_NormalisesGbxWithMultiplier_AndConvertsToTarget()
    {
        var asOf = new DateTime(2024, 6, 3);
        _listingRepository.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(Listing("GBX", 0.01m));
        _priceSource
            .Setup(x => x.GetDailySeries("CSPX.LON", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Price(500m, asOf)]); // 500 GBX
        // Normalised 5 GBP → USD at 1.25 = 6.25
        _currencyExchangeService
            .Setup(x => x.GetExchangeRateAsync(It.Is<Currency>(c => c.ShortName == "GBP"), It.Is<Currency>(c => c.ShortName == "USD"), It.IsAny<DateTime>()))
            .ReturnsAsync(1.25m);

        var result = await CreateSut().GetPricePerUnitAsync(10, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(6.25m, result);
        var stored = Assert.Single(_priceQuoteRepository.All);
        Assert.Equal(5m, stored.Price);
        Assert.Equal("GBP", stored.Currency);
        Assert.Equal(500m, stored.RawPrice);
        Assert.Equal("GBX", stored.RawCurrency);
    }

    [Fact]
    public async Task GetPricePerUnit_ReturnsZero_WhenNoSymbolConfigured()
    {
        var asOf = new DateTime(2024, 6, 3);
        var listing = Listing();
        listing.MarketDataSymbols.Clear();
        _listingRepository.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(listing);

        var result = await CreateSut().GetPricePerUnitAsync(10, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(0m, result);
        Assert.Empty(_priceQuoteRepository.All);
    }

    [Fact]
    public async Task GetPricePerUnitSeries_CarriesLastKnownPriceForward_OnDaysWithoutQuote()
    {
        var mon = new DateTime(2024, 1, 1); // Monday
        var wed = new DateTime(2024, 1, 3);
        var fri = new DateTime(2024, 1, 5);
        _listingRepository.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(Listing());
        _priceSource
            .Setup(x => x.GetDailySeries("CSPX.LON", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Price(100m, mon), Price(110m, wed)]);

        var series = await CreateSut().GetPricePerUnitSeriesAsync(10, _usd, mon, fri, TestContext.Current.CancellationToken);

        Assert.Equal(100m, series[mon]);
        Assert.Equal(100m, series[new DateTime(2024, 1, 2)]); // Tue carries Monday forward
        Assert.Equal(110m, series[wed]);
        Assert.Equal(110m, series[new DateTime(2024, 1, 4)]); // Thu carries Wednesday forward
        Assert.Equal(110m, series[fri]);
    }

    /// <summary>Minimal in-memory <see cref="IPriceQuoteRepository"/> so fetch→store→read flows are exercised end-to-end.</summary>
    private sealed class InMemoryPriceQuoteRepository : IPriceQuoteRepository
    {
        private readonly List<PriceQuote> _quotes = [];
        private long _nextId = 1;

        public IReadOnlyList<PriceQuote> All => _quotes;

        public void Seed(PriceQuote quote) => _quotes.Add(quote);

        public Task<PriceQuote?> Get(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_quotes.FirstOrDefault(x => x.Id == id));

        public Task<PriceQuote?> GetLatestOnOrBefore(long assetListingId, DateTimeOffset asOf, MarketDataProvider? provider = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_quotes
                .Where(x => x.AssetListingId == assetListingId && x.PriceTime <= asOf && (provider is null || x.Provider == provider))
                .OrderByDescending(x => x.PriceTime)
                .FirstOrDefault());

        public Task<IReadOnlyList<PriceQuote>> GetRange(long assetListingId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PriceQuote>>(_quotes
                .Where(x => x.AssetListingId == assetListingId && x.PriceTime >= start && x.PriceTime <= end)
                .OrderBy(x => x.PriceTime)
                .ToList());

        public Task<PriceQuote> Add(PriceQuote quote, CancellationToken cancellationToken = default)
        {
            quote.Id = _nextId++;
            _quotes.Add(quote);
            return Task.FromResult(quote);
        }

        public Task<PriceQuote> Upsert(PriceQuote quote, CancellationToken cancellationToken = default)
        {
            var existing = _quotes.FirstOrDefault(x =>
                x.AssetListingId == quote.AssetListingId
                && x.Provider == quote.Provider
                && x.PriceTime == quote.PriceTime
                && x.QuoteType == quote.QuoteType);

            if (existing is not null)
            {
                existing.Price = quote.Price;
                existing.Currency = quote.Currency;
                existing.RawPrice = quote.RawPrice;
                existing.RawCurrency = quote.RawCurrency;
                existing.MarketDataSymbolId = quote.MarketDataSymbolId;
                return Task.FromResult(existing);
            }

            return Add(quote, cancellationToken);
        }

        public Task<bool> Delete(long id, CancellationToken cancellationToken = default)
        {
            var removed = _quotes.RemoveAll(x => x.Id == id);
            return Task.FromResult(removed > 0);
        }
    }
}