using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Features.Assets.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

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

    [Fact]
    public async Task GetHoldingsByAccountAsOf_GroupsByAccountAndListing_AndNetsSignedQuantities()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);
        var txRepo = new InvestmentTransactionRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);
        var listing1 = await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD"
        }, ct);
        var listing2 = await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "SXR8",
            ExchangeMic = "XETR",
            ExchangeName = "Xetra",
            TradingCurrency = "EUR"
        }, ct);

        // Account 10: Buy 5 CSPX on Jan 10, Sell 2 CSPX on Jan 15, Buy 10 SXR8 on Jan 12
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = 10,
            AssetListingId = listing1.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 5.5m,
            UnitPrice = 500m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10)
        }, ct);
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = 10,
            AssetListingId = listing1.Id,
            Type = InvestmentTransactionType.Sell,
            Quantity = 2.25m,
            UnitPrice = 510m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 15)
        }, ct);
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = 10,
            AssetListingId = listing2.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 10m,
            UnitPrice = 450m,
            Currency = "EUR",
            TradeDate = new DateOnly(2026, 1, 12)
        }, ct);

        // Account 20: Buy 1 CSPX on Jan 10, Buy 2 CSPX on Jan 25 (after as-of)
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = 20,
            AssetListingId = listing1.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 1m,
            UnitPrice = 500m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10)
        }, ct);
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = 20,
            AssetListingId = listing1.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 2m,
            UnitPrice = 500m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 25)
        }, ct);

        // Account 30 (not requested): Buy 100 CSPX
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = 30,
            AssetListingId = listing1.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 100m,
            UnitPrice = 500m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10)
        }, ct);

        var asOf = new DateOnly(2026, 1, 20);
        var holdings = await txRepo.GetHoldingsByAccountAsOf([10, 20], asOf, ct);

        Assert.Equal(2, holdings.Count);
        Assert.True(holdings.ContainsKey(10));
        Assert.True(holdings.ContainsKey(20));
        Assert.False(holdings.ContainsKey(30));

        Assert.Equal(3.25m, holdings[10][listing1.Id]); // 5.5 - 2.25
        Assert.Equal(10m, holdings[10][listing2.Id]);
        Assert.Equal(1m, holdings[20][listing1.Id]); // Jan 25 trade excluded
    }

    [Fact]
    public async Task GetValuationInputs_SplitsOpeningAndInWindow_AndExcludesOutOfRange()
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

        const int accountId = 10;
        // Opening before window (window: Jan 10 to Jan 20)
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 5m,
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 1)
        }, ct);
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Sell,
            Quantity = 1m,
            UnitPrice = 105m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 5)
        }, ct);

        // In-window trades (Jan 10, Jan 15)
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 2m,
            UnitPrice = 110m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10)
        }, ct);
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Sell,
            Quantity = 0.5m,
            UnitPrice = 115m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 15)
        }, ct);

        // After window (Jan 25)
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 20m,
            UnitPrice = 120m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 25)
        }, ct);

        var inputs = await txRepo.GetValuationInputs(
            [accountId],
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 20),
            ct);

        Assert.Single(inputs.OpeningPositions);
        Assert.Equal(accountId, inputs.OpeningPositions[0].AccountId);
        Assert.Equal(listing.Id, inputs.OpeningPositions[0].AssetListingId);
        Assert.Equal(4m, inputs.OpeningPositions[0].Quantity); // 5 - 1

        Assert.Equal(2, inputs.InWindowTrades.Count);
        var trade1 = inputs.InWindowTrades.Single(t => t.TradeDate == new DateOnly(2026, 1, 10));
        Assert.Equal(2m, trade1.SignedQuantity);
        var trade2 = inputs.InWindowTrades.Single(t => t.TradeDate == new DateOnly(2026, 1, 15));
        Assert.Equal(-0.5m, trade2.SignedQuantity);
    }

    [Fact]
    public async Task GetCapitalFlowInputs_ProjectsBoundedFields_AndIncludesPriceMultiplier()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);
        var txRepo = new InvestmentTransactionRepository(ctx);

        var asset = await assetRepo.Add(MakeISharesAsset(), ct);
        var gbxListing = await listingRepo.Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSP1",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "GBX",
            PriceMultiplier = 0.01m
        }, ct);

        const int accountId = 10;
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = gbxListing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 10m,
            UnitPrice = 40000m,
            Fee = 500m,
            Currency = "GBX",
            TradeDate = new DateOnly(2026, 1, 10)
        }, ct);
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = gbxListing.Id,
            Type = InvestmentTransactionType.Sell,
            Quantity = 5m,
            UnitPrice = 42000m,
            Fee = 250m,
            Currency = "GBX",
            TradeDate = new DateOnly(2026, 1, 15)
        }, ct);
        // After toDate
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = gbxListing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 1m,
            UnitPrice = 43000m,
            Currency = "GBX",
            TradeDate = new DateOnly(2026, 1, 25)
        }, ct);

        var flows = await txRepo.GetCapitalFlowInputs([accountId], new DateOnly(2026, 1, 20), ct);

        Assert.Equal(2, flows.Count);

        var buyFlow = flows[0];
        Assert.Equal(accountId, buyFlow.AccountId);
        Assert.Equal(new DateOnly(2026, 1, 10), buyFlow.TradeDate);
        Assert.Equal(InvestmentTransactionType.Buy, buyFlow.Type);
        Assert.Equal(10m, buyFlow.Quantity);
        Assert.Equal(40000m, buyFlow.UnitPrice);
        Assert.Equal(500m, buyFlow.Fee);
        Assert.Equal("GBX", buyFlow.Currency);
        Assert.Equal(0.01m, buyFlow.ListingPriceMultiplier);

        var sellFlow = flows[1];
        Assert.Equal(accountId, sellFlow.AccountId);
        Assert.Equal(new DateOnly(2026, 1, 15), sellFlow.TradeDate);
        Assert.Equal(InvestmentTransactionType.Sell, sellFlow.Type);
        Assert.Equal(5m, sellFlow.Quantity);
        Assert.Equal(42000m, sellFlow.UnitPrice);
        Assert.Equal(250m, sellFlow.Fee);
        Assert.Equal("GBX", sellFlow.Currency);
        Assert.Equal(0.01m, sellFlow.ListingPriceMultiplier);
    }
}