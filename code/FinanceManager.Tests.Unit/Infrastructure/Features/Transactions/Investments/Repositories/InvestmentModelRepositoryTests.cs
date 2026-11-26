using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Infrastructure.Features.Assets.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Transactions.Investments.Repositories;

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

        var symbols = await symbolRepo.GetByListing(listing.Id, ct);
        Assert.Contains(symbols, symbol => symbol.Provider == MarketDataProvider.AlphaVantage && symbol.IsPrimary && symbol.Symbol == "CSPX.LON");

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

        var holdingsMid = await txRepo.GetHoldingsByUserAsOf(1, new DateOnly(2026, 1, 20), ct);
        Assert.Equal(3.25m, Assert.Single(holdingsMid, holding => holding.AssetListingId == listing.Id).Units);

        var holdingsAfter = await txRepo.GetHoldingsByUserAsOf(1, new DateOnly(2026, 3, 1), ct);
        Assert.Equal(2.00m, Assert.Single(holdingsAfter, holding => holding.AssetListingId == listing.Id).Units);
    }

    [Fact]
    public async Task GetByUserAndIds_FiltersOwnerAndIdsBeforeMaterializing_IncludingUnlinkedTrades()
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

        InvestmentTransaction Make(long userId, int? accountId, DateOnly tradeDate) => new()
        {
            UserId = userId,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 1m,
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = tradeDate
        };

        var linked = await txRepo.Add(Make(1, 10, new DateOnly(2026, 1, 10)), ct);
        var unlinked = await txRepo.Add(Make(1, null, new DateOnly(2026, 1, 9)), ct);
        var guest = await txRepo.Add(Make(-4, null, new DateOnly(2026, 1, 8)), ct);
        var otherUser = await txRepo.Add(Make(2, 20, new DateOnly(2026, 1, 11)), ct);
        long[] everyId = [linked.EntryId, unlinked.EntryId, guest.EntryId, otherUser.EntryId];

        var result = await txRepo.GetByUserAndIds(1, everyId, ct);

        Assert.Equal([unlinked.EntryId, linked.EntryId], result.Select(transaction => transaction.EntryId));
        Assert.Equal(guest.EntryId, Assert.Single(await txRepo.GetByUserAndIds(-4, everyId, ct)).EntryId);
        Assert.Empty(await txRepo.GetByUserAndIds(1, [], ct));
        Assert.Empty(await txRepo.GetByUserAndIds(0, everyId, ct));
    }

    [Fact]
    public async Task GetCapitalFlowInputsByUser_PoolsLinkedAndUnlinkedTrades_AndIsolatesOwners()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var asset = await new AssetRepository(ctx).Add(MakeISharesAsset(), ct);
        var listing = await new AssetListingRepository(ctx).Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "CSP1",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "GBX",
            PriceMultiplier = 0.01m
        }, ct);
        var repository = new InvestmentTransactionRepository(ctx);

        InvestmentTransaction Make(long userId, int? accountId, InvestmentTransactionType type, DateOnly tradeDate) => new()
        {
            UserId = userId,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = type,
            Quantity = 2m,
            UnitPrice = 100m,
            Fee = 1m,
            Currency = "GBX",
            TradeDate = tradeDate
        };

        var second = await repository.Add(Make(-4, null, InvestmentTransactionType.Buy, new DateOnly(2026, 1, 12)), ct);
        var first = await repository.Add(Make(-4, 10, InvestmentTransactionType.Buy, new DateOnly(2026, 1, 10)), ct);
        var sell = await repository.Add(Make(-4, 11, InvestmentTransactionType.Sell, new DateOnly(2026, 1, 12)), ct);
        await repository.Add(Make(-4, null, InvestmentTransactionType.Buy, new DateOnly(2026, 1, 25)), ct);
        await repository.Add(Make(1, 10, InvestmentTransactionType.Buy, new DateOnly(2026, 1, 10)), ct);

        var flows = await repository.GetCapitalFlowInputsByUser(-4, new DateOnly(2026, 1, 20), ct);

        // Guest ids are negative; trades pool across accounts and unlinked rows in posting-date then id order and never cross owners.
        Assert.Equal([first.EntryId, second.EntryId, sell.EntryId], flows.Select(flow => flow.TransactionId));
        Assert.Equal(new DateOnly(2026, 1, 10), flows[0].TradeDate);
        Assert.Empty(await repository.GetCapitalFlowInputsByUser(0, new DateOnly(2026, 1, 20), ct));
        Assert.Single(await repository.GetCapitalFlowInputsByUser(1, new DateOnly(2026, 1, 20), ct));
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
    public async Task GetHistoryPage_ReturnsDeterministicOrder_TradeDateDesc_Then_IdDesc()
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

        const int accountId = 100;
        var t1 = await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 1m,
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10)
        }, ct);
        var t2 = await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 2m,
            UnitPrice = 101m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 20)
        }, ct);
        var t3 = await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Sell,
            Quantity = 1m,
            UnitPrice = 102m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 20)
        }, ct);

        var (items, hasMore) = await txRepo.GetHistoryPageByUser(1, pageSize: 10, cancellationToken: ct);

        Assert.False(hasMore);
        Assert.Equal(3, items.Count);
        // t3 and t2 are on 2026-01-20, t3 was added after t2 so t3.Id > t2.Id
        Assert.Equal(t3.EntryId, items[0].EntryId);
        Assert.Equal(t2.EntryId, items[1].EntryId);
        Assert.Equal(t1.EntryId, items[2].EntryId);
    }

    [Fact]
    public async Task GetHistoryPage_SupportsCursorContinuation_AcrossSameDayBoundaries()
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

        const int accountId = 101;
        var sameDay = new DateOnly(2026, 3, 15);
        var t1 = await txRepo.Add(new InvestmentTransaction { UserId = 1, AccountId = accountId, AssetListingId = listing.Id, Type = InvestmentTransactionType.Buy, Quantity = 1m, UnitPrice = 100m, Currency = "USD", TradeDate = sameDay }, ct);
        var t2 = await txRepo.Add(new InvestmentTransaction { UserId = 1, AccountId = accountId, AssetListingId = listing.Id, Type = InvestmentTransactionType.Buy, Quantity = 2m, UnitPrice = 100m, Currency = "USD", TradeDate = sameDay }, ct);
        var t3 = await txRepo.Add(new InvestmentTransaction { UserId = 1, AccountId = accountId, AssetListingId = listing.Id, Type = InvestmentTransactionType.Buy, Quantity = 3m, UnitPrice = 100m, Currency = "USD", TradeDate = sameDay }, ct);
        var t4 = await txRepo.Add(new InvestmentTransaction { UserId = 1, AccountId = accountId, AssetListingId = listing.Id, Type = InvestmentTransactionType.Buy, Quantity = 4m, UnitPrice = 100m, Currency = "USD", TradeDate = sameDay }, ct);

        // Page 1 with pageSize = 2
        var (page1, hasMore1) = await txRepo.GetHistoryPageByUser(1, pageSize: 2, cancellationToken: ct);
        Assert.True(hasMore1);
        Assert.Equal(2, page1.Count);
        Assert.Equal(t4.EntryId, page1[0].EntryId);
        Assert.Equal(t3.EntryId, page1[1].EntryId);

        // Page 2 continuing from page 1's last item (t3)
        var (page2, hasMore2) = await txRepo.GetHistoryPageByUser(
            1, pageSize: 2, cursorTradeDate: page1[^1].TradeDate, cursorId: page1[^1].EntryId, cancellationToken: ct);
        Assert.False(hasMore2);
        Assert.Equal(2, page2.Count);
        Assert.Equal(t2.EntryId, page2[0].EntryId);
        Assert.Equal(t1.EntryId, page2[1].EntryId);
    }

    [Fact]
    public async Task GetHistoryPage_AppliesFilters_DateRange_Type_Search()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var assetRepo = new AssetRepository(ctx);
        var listingRepo = new AssetListingRepository(ctx);
        var txRepo = new InvestmentTransactionRepository(ctx);

        var asset1 = await assetRepo.Add(MakeISharesAsset(), ct);
        var listing1 = await listingRepo.Add(new AssetListing
        {
            AssetId = asset1.Id,
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            ExchangeName = "London Stock Exchange",
            TradingCurrency = "USD"
        }, ct);

        var asset2 = await assetRepo.Add(new Asset
        {
            Name = "Apple Inc.",
            Type = AssetType.Stock,
            Isin = "US0378331005"
        }, ct);
        var listing2 = await listingRepo.Add(new AssetListing
        {
            AssetId = asset2.Id,
            Ticker = "AAPL",
            ExchangeMic = "XNAS",
            ExchangeName = "NASDAQ",
            TradingCurrency = "USD"
        }, ct);

        const int accountId = 102;
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing1.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 10m,
            UnitPrice = 500m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10),
            Description = "Monthly ETF contribution"
        }, ct);

        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing2.Id,
            Type = InvestmentTransactionType.Sell,
            Quantity = 5m,
            UnitPrice = 220m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 2, 1),
            Description = "Tech trim"
        }, ct);

        // Filter by Date Range
        var (dateFiltered, _) = await txRepo.GetHistoryPageByUser(
            1, 10, startDate: new DateOnly(2026, 1, 1), endDate: new DateOnly(2026, 1, 31), cancellationToken: ct);
        Assert.Single(dateFiltered);
        Assert.Equal("CSPX", dateFiltered[0].AssetListing.Ticker);

        // Filter by Type
        var (typeFiltered, _) = await txRepo.GetHistoryPageByUser(
            1, 10, type: InvestmentTransactionType.Sell, cancellationToken: ct);
        Assert.Single(typeFiltered);
        Assert.Equal("AAPL", typeFiltered[0].AssetListing.Ticker);

        // Filter by Search on Notes
        var (noteSearch, _) = await txRepo.GetHistoryPageByUser(1, 10, search: "ETF contribution", cancellationToken: ct);
        Assert.Single(noteSearch);
        Assert.Equal("CSPX", noteSearch[0].AssetListing.Ticker);

        // Filter by Search on Ticker
        var (tickerSearch, _) = await txRepo.GetHistoryPageByUser(1, 10, search: "AAPL", cancellationToken: ct);
        Assert.Single(tickerSearch);
        Assert.Equal("AAPL", tickerSearch[0].AssetListing.Ticker);

        // Filter by Search on Asset Name, case-insensitively
        var (nameSearch, _) = await txRepo.GetHistoryPageByUser(1, 10, search: "apple", cancellationToken: ct);
        Assert.Single(nameSearch);
        Assert.Equal("Apple Inc.", nameSearch[0].AssetListing.Asset.Name);

        // Filter by Search on ISIN
        var (isinSearch, _) = await txRepo.GetHistoryPageByUser(1, 10, search: "US0378331005", cancellationToken: ct);
        Assert.Single(isinSearch);
        Assert.Equal("AAPL", isinSearch[0].AssetListing.Ticker);

        // Filter by Search on Currency and Exchange
        var (currencySearch, _) = await txRepo.GetHistoryPageByUser(1, 10, search: "usd", cancellationToken: ct);
        Assert.Equal(2, currencySearch.Count);
        var (exchangeSearch, _) = await txRepo.GetHistoryPageByUser(1, 10, search: "nasdaq", cancellationToken: ct);
        Assert.Single(exchangeSearch);
        Assert.Equal("AAPL", exchangeSearch[0].AssetListing.Ticker);

        // Wildcards in the user's term are treated as literal text.
        var (wildcardSearch, _) = await txRepo.GetHistoryPageByUser(1, 10, search: "%", cancellationToken: ct);
        Assert.Empty(wildcardSearch);
    }

    [Fact]
    public async Task GetHistoryPage_EmptyPage_WhenNoMatchingRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var txRepo = new InvestmentTransactionRepository(ctx);

        var (items, hasMore) = await txRepo.GetHistoryPageByUser(999, 10, cancellationToken: ct);
        Assert.Empty(items);
        Assert.False(hasMore);

        var (zeroPage, zeroHasMore) = await txRepo.GetHistoryPageByUser(999, 0, cancellationToken: ct);
        Assert.Empty(zeroPage);
        Assert.False(zeroHasMore);
    }

    [Fact]
    public async Task GetHistoryPage_EditsAndDeletesCompatibility()
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

        const int accountId = 103;
        var t1 = await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = accountId,
            AssetListingId = listing.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 10m,
            UnitPrice = 500m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10),
            Description = "Original Note"
        }, ct);

        // Update note
        t1.Description = "Updated Note";
        await txRepo.Update(t1, ct);

        var (afterUpdate, _) = await txRepo.GetHistoryPageByUser(1, 10, cancellationToken: ct);
        Assert.Single(afterUpdate);
        Assert.Equal("Updated Note", afterUpdate[0].Description);

        // Delete
        await txRepo.Delete(t1.EntryId, ct);
        var (afterDelete, _) = await txRepo.GetHistoryPageByUser(1, 10, cancellationToken: ct);
        Assert.Empty(afterDelete);
    }
    [Fact]
    public async Task GetHoldingsByUserAsOf_PoolsAccountsPerListing_AndNetsSignedQuantities()
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

        // Another owner's account 30: Buy 100 CSPX
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 2,
            AccountId = 30,
            AssetListingId = listing1.Id,
            Type = InvestmentTransactionType.Buy,
            Quantity = 100m,
            UnitPrice = 500m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10)
        }, ct);

        // A transaction type added by a newer schema must not be interpreted as a Buy by an older
        // reader, otherwise holdings can move without a corresponding capital event.
        await txRepo.Add(new InvestmentTransaction
        {
            UserId = 1,
            AccountId = 10,
            AssetListingId = listing1.Id,
            Type = (InvestmentTransactionType)999,
            Quantity = 100m,
            UnitPrice = 1m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, 10)
        }, ct);

        var asOf = new DateOnly(2026, 1, 20);
        var holdings = (await txRepo.GetHoldingsByUserAsOf(1, asOf, ct)).ToDictionary(h => h.AssetListingId, h => h.Units);

        Assert.Equal(2, holdings.Count);
        Assert.Equal(4.25m, holdings[listing1.Id]); // (5.5 - 2.25) in account 10 + 1 in account 20; the Jan 25 trade and the other owner's trade are excluded
        Assert.Equal(10m, holdings[listing2.Id]);
    }

    [Fact]
    public async Task GetValuationInputsByUser_SplitsOpeningAndInWindow_AndExcludesOutOfRange()
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

        var inputs = await txRepo.GetValuationInputsByUser(
            1,
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 20),
            ct);

        Assert.Single(inputs.OpeningPositions);
        Assert.Equal(listing.Id, inputs.OpeningPositions[0].AssetListingId);
        Assert.Equal(4m, inputs.OpeningPositions[0].Quantity); // 5 - 1

        Assert.Equal(2, inputs.InWindowTrades.Count);
        var trade1 = inputs.InWindowTrades.Single(t => t.TradeDate == new DateOnly(2026, 1, 10));
        Assert.Equal(2m, trade1.SignedQuantity);
        var trade2 = inputs.InWindowTrades.Single(t => t.TradeDate == new DateOnly(2026, 1, 15));
        Assert.Equal(-0.5m, trade2.SignedQuantity);
    }

    [Fact]
    public async Task GetCapitalFlowInputsByUser_PreservesTransactionAmountsAfterListingCurrencyEnrichment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var asset = await new AssetRepository(context).Add(MakeISharesAsset(), ct);
        var listing = await new AssetListingRepository(context).Add(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = "VWCE",
            TradingCurrency = null
        }, ct);
        var repository = new InvestmentTransactionRepository(context);
        foreach (var type in new[] { InvestmentTransactionType.Buy, InvestmentTransactionType.Sell })
        {
            await repository.Add(new InvestmentTransaction
            {
                UserId = 1,
                AccountId = 10,
                AssetListingId = listing.Id,
                Type = type,
                Quantity = 2m,
                UnitPrice = 100m,
                Fee = 5m,
                Currency = "CHF",
                TradeDate = new DateOnly(2026, 1, 10)
            }, ct);
        }

        var before = await repository.GetCapitalFlowInputsByUser(1, new DateOnly(2026, 1, 20), ct);
        listing.ExchangeMic = "XETR";
        listing.TradingCurrency = "EUR";
        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
        var after = await repository.GetCapitalFlowInputsByUser(1, new DateOnly(2026, 1, 20), ct);

        Assert.Equal(2, before.Count);
        Assert.All(before, flow => Assert.Equal("CHF", flow.Currency));
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task GetCapitalFlowInputsByUser_ProjectsBoundedFields_AndIncludesPriceMultiplier()
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

        var flows = await txRepo.GetCapitalFlowInputsByUser(1, new DateOnly(2026, 1, 20), ct);

        Assert.Equal(2, flows.Count);

        var buyFlow = flows[0];
        Assert.Equal(gbxListing.Id, buyFlow.AssetListingId);
        Assert.Equal(new DateOnly(2026, 1, 10), buyFlow.TradeDate);
        Assert.Equal(InvestmentTransactionType.Buy, buyFlow.Type);
        Assert.Equal(10m, buyFlow.Quantity);
        Assert.Equal(40000m, buyFlow.UnitPrice);
        Assert.Equal(500m, buyFlow.Fee);
        Assert.Equal("GBX", buyFlow.Currency);

        var sellFlow = flows[1];
        Assert.Equal(gbxListing.Id, sellFlow.AssetListingId);
        Assert.Equal(new DateOnly(2026, 1, 15), sellFlow.TradeDate);
        Assert.Equal(InvestmentTransactionType.Sell, sellFlow.Type);
        Assert.Equal(5m, sellFlow.Quantity);
        Assert.Equal(42000m, sellFlow.UnitPrice);
        Assert.Equal(250m, sellFlow.Fee);
        Assert.Equal("GBX", sellFlow.Currency);
    }
}