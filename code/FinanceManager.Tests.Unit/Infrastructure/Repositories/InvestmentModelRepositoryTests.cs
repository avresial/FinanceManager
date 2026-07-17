using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Repositories.Assets;
using FinanceManager.Infrastructure.Repositories.Investments;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// Covers the issue #474 acceptance criteria for the new investment model: creating assets,
/// multiple listings per asset, provider symbols, buy/sell transactions against a listing, and
/// cached price quotes. Uses the EF Core in-memory provider (note: it ignores unique indexes,
/// so duplicate-rejection is exercised at the repository-upsert level rather than via the DB).
/// </summary>
[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class InvestmentModelRepositoryTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Asset MakeISharesAsset() => new()
    {
        Name = "iShares Core S&P 500 UCITS ETF USD (Acc)",
        Type = AssetType.ETF,
        Isin = "IE00B5BMR087",
        Issuer = "iShares",
        BaseCurrency = "USD",
        DistributionPolicy = DistributionPolicy.Accumulating,
        BenchmarkIndex = "S&P 500",
        IsUcits = true
    };

    [Fact]
    public async Task Add_CreatesAsset_AndAssignsId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var repo = new AssetRepository(ctx);

        var asset = await repo.Add(MakeISharesAsset(), ct);

        Assert.True(asset.Id > 0);
        Assert.NotEqual(default, asset.CreatedAt);
        Assert.NotEqual(default, asset.UpdatedAt);

        var reloaded = await repo.GetByIsin("IE00B5BMR087", ct);
        Assert.NotNull(reloaded);
        Assert.Equal(AssetType.ETF, reloaded.Type);
        Assert.Equal(DistributionPolicy.Accumulating, reloaded.DistributionPolicy);
    }

    [Fact]
    public async Task Upsert_UpdatesExistingAsset_MatchedByIsin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var repo = new AssetRepository(ctx);

        var first = await repo.Add(MakeISharesAsset(), ct);

        var update = MakeISharesAsset();
        update.Issuer = "BlackRock";
        var upserted = await repo.Upsert(update, ct);

        Assert.Equal(first.Id, upserted.Id);
        Assert.Equal("BlackRock", upserted.Issuer);
        Assert.Single(await repo.GetAll(ct));
    }

    [Fact]
    public async Task Upsert_MatchesExistingAsset_ByShareClassFigi_WhenNoIsin()
    {
        // FIGI-centric identity: an asset with no ISIN is matched and updated by its share-class FIGI,
        // not duplicated. A later ISIN is backfilled onto the same row.
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var repo = new AssetRepository(ctx);

        var first = await repo.Add(new Asset
        {
            Name = "Microsoft Corp",
            Type = AssetType.Stock,
            ShareClassFigi = "BBG001S5TD05"
        }, ct);

        var update = new Asset
        {
            Name = "Microsoft Corporation",
            Type = AssetType.Stock,
            ShareClassFigi = "BBG001S5TD05",
            Isin = "US5949181045"
        };
        var upserted = await repo.Upsert(update, ct);

        Assert.Equal(first.Id, upserted.Id);
        Assert.Equal("Microsoft Corporation", upserted.Name);
        Assert.Equal("US5949181045", upserted.Isin);
        Assert.Single(await repo.GetAll(ct));

        var byFigi = await repo.GetByShareClassFigi("BBG001S5TD05", ct);
        Assert.NotNull(byFigi);
        Assert.Equal(first.Id, byFigi.Id);
    }

    [Fact]
    public async Task Upsert_MatchedByIsin_DoesNotClearExistingShareClassFigi()
    {
        // An ISIN-only update (no incoming FIGI) must never wipe the canonical share-class FIGI on the
        // row it matches — the key is authoritative and uniquely indexed.
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var repo = new AssetRepository(ctx);

        var first = await repo.Add(new Asset
        {
            Name = "Microsoft Corp",
            Type = AssetType.Stock,
            Isin = "US5949181045",
            ShareClassFigi = "BBG001S5TD05"
        }, ct);

        var isinOnlyUpdate = new Asset
        {
            Name = "Microsoft Corporation",
            Type = AssetType.Stock,
            Isin = "US5949181045",
            ShareClassFigi = null
        };
        var upserted = await repo.Upsert(isinOnlyUpdate, ct);

        Assert.Equal(first.Id, upserted.Id);
        Assert.Equal("Microsoft Corporation", upserted.Name);
        Assert.Equal("BBG001S5TD05", upserted.ShareClassFigi);
        Assert.Single(await repo.GetAll(ct));
    }

    [Fact]
    public async Task Asset_CanHaveMultipleListings_IncludingGbxMultiplier()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);

        await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD",
            IsPrimaryListing = true
        }, ct);
        await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSP1",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "GBX",
            PriceMultiplier = 0.01m
        }, ct);
        await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "SXR8",
            ExchangeMic = "XETR",
            ExchangeName = "Xetra",
            TradingCurrency = "EUR"
        }, ct);

        var listings = await listingRepo.GetByAsset(asset.Id, ct);
        Assert.Equal(3, listings.Count);

        var gbx = listings.Single(x => x.Ticker == "CSP1");
        Assert.Equal(0.01m, gbx.PriceMultiplier);
        Assert.Equal("GBX", gbx.TradingCurrency);
    }

    [Fact]
    public async Task AddListing_RejectsNonPositivePriceMultiplier()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);
        var invalid = new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD",
            PriceMultiplier = 0m
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => listingRepo.Add(invalid, ct));
    }

    [Fact]
    public async Task Listing_CanHaveProviderSymbols()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);
        var symbolRepo = new MarketDataSymbolRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);
        var listing = await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD"
        }, ct);

        await symbolRepo.Add(new MarketDataSymbol
        {
            AssetListingId = listing.Id,
            Provider = MarketDataProvider.AlphaVantage,
            Symbol = "CSPX.LON",
            IsPrimary = true
        }, ct);
        await symbolRepo.Add(new MarketDataSymbol
        {
            AssetListingId = listing.Id,
            Provider = MarketDataProvider.Eodhd,
            Symbol = "CSPX.LSE"
        }, ct);

        var primaryAv = await symbolRepo.GetPrimary(listing.Id, MarketDataProvider.AlphaVantage, ct);
        Assert.NotNull(primaryAv);
        Assert.Equal("CSPX.LON", primaryAv.Symbol);

        Assert.Equal(2, (await symbolRepo.GetByListing(listing.Id, ct)).Count);
    }

    [Fact]
    public async Task BuyAndSell_Transactions_ComputeSignedHoldings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);
        var txRepo = new InvestmentTransactionRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);
        var listing = await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD"
        }, ct);

        const int accountId = 42;
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 3.25m,
            UnitPrice = 520.45m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10),
            Fee = 1.00m
        }, ct);
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Sell,
            Quantity = 1.25m,
            UnitPrice = 540.00m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 2, 15)
        }, ct);

        var holdingsMid = await txRepo.GetHoldingsAsOf([accountId], new DateOnly(2026, 1, 20), ct);
        Assert.Equal(3.25m, holdingsMid[listing.Id]);

        var holdingsAfter = await txRepo.GetHoldingsAsOf([accountId], new DateOnly(2026, 3, 1), ct);
        Assert.Equal(2.00m, holdingsAfter[listing.Id]);
    }

    [Fact]
    public async Task GetByAccounts_ReturnsTransactionsForRequestedAccountsOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);
        var txRepo = new InvestmentTransactionRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);
        var listing = await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD"
        }, ct);

        InvestmentTransaction Make(int accountId, DateOnly tradeDate) => new()
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 1m,
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = tradeDate
        };

        await txRepo.Add(Make(10, new DateOnly(2026, 1, 10)), ct);
        await txRepo.Add(Make(20, new DateOnly(2026, 1, 11)), ct);
        await txRepo.Add(Make(30, new DateOnly(2026, 1, 12)), ct); // not requested

        var result = await txRepo.GetByAccounts([10, 20], ct);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.AccountId == 10);
        Assert.Contains(result, t => t.AccountId == 20);
        Assert.DoesNotContain(result, t => t.AccountId == 30);
        Assert.NotNull(result[0].AssetListing); // AssetListing eagerly included for downstream pricing

        Assert.Empty(await txRepo.GetByAccounts([], ct));
    }

    [Fact]
    public async Task PriceQuote_StoreAndRead_LatestOnOrBefore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);
        var quoteRepo = new PriceQuoteRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);
        var listing = await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSP1",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "GBX",
            PriceMultiplier = 0.01m
        }, ct);

        // Raw GBX 41000 pence normalises to 410.00 GBP via the listing multiplier.
        await quoteRepo.Add(new PriceQuote
        {
            AssetListingId = listing.Id,
            Provider = MarketDataProvider.AlphaVantage,
            Price = 410.00m,
            Currency = "GBP",
            RawPrice = 41000m,
            RawCurrency = "GBX",
            PriceTime = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
            QuoteType = PriceQuoteType.EndOfDay
        }, ct);
        await quoteRepo.Add(new PriceQuote
        {
            AssetListingId = listing.Id,
            Provider = MarketDataProvider.AlphaVantage,
            Price = 415.00m,
            Currency = "GBP",
            PriceTime = new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero),
            QuoteType = PriceQuoteType.EndOfDay
        }, ct);

        var latest = await quoteRepo.GetLatestOnOrBefore(
            listing.Id, new DateTimeOffset(2026, 1, 11, 0, 0, 0, TimeSpan.Zero), cancellationToken: ct);

        Assert.NotNull(latest);
        Assert.Equal(410.00m, latest.Price);
        Assert.Equal(41000m, latest.RawPrice);
    }

    [Fact]
    public async Task PriceQuote_Upsert_ReplacesSameListingProviderTimeType()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);
        var quoteRepo = new PriceQuoteRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);
        var listing = await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD"
        }, ct);

        var time = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        PriceQuote MakeQuote(decimal price) => new()
        {
            AssetListingId = listing.Id,
            Provider = MarketDataProvider.AlphaVantage,
            Price = price,
            Currency = "USD",
            PriceTime = time,
            QuoteType = PriceQuoteType.EndOfDay
        };

        await quoteRepo.Upsert(MakeQuote(500m), ct);
        await quoteRepo.Upsert(MakeQuote(505m), ct);

        var range = await quoteRepo.GetRange(listing.Id, time, time, ct);
        Assert.Single(range);
        Assert.Equal(505m, range[0].Price);
    }
}