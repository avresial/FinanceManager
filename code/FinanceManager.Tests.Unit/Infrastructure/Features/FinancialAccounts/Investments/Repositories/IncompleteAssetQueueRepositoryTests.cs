using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Features.Assets.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

[Trait("Category", "Unit")]
public class IncompleteAssetQueueRepositoryTests
{
    private static readonly DateTimeOffset _cutoff = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Queue_PagesFiltersAndCountsCurrentHolders_WithRelationalQuery()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = new QueueContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var healthy = Asset("A healthy", "HEALTHY");
        healthy.Listings.Single().MarketDataSymbols.Add(Symbol(_cutoff, null));
        healthy.Listings.Single().MarketDataSymbols.Add(Symbol(_cutoff.AddDays(-1), "alternative failed", MarketDataProvider.TwelveData));
        var missing = Asset("B missing", "MISSING");
        missing.Type = AssetType.Other;
        missing.Isin = null;
        missing.Listings.Single().ExchangeMic = null;
        missing.Listings.Single().TradingCurrency = null;
        var stale = Asset("C stale", "STALE");
        stale.Listings.Single().MarketDataSymbols.Add(Symbol(_cutoff.AddTicks(-1), null));
        var failing = Asset("D failing", "FAILING");
        failing.Listings.Single().MarketDataSymbols.Add(Symbol(_cutoff, "request failed"));
        var disabled = Symbol(_cutoff, null, MarketDataProvider.TwelveData);
        disabled.IsEnabled = false;
        failing.Listings.Single().MarketDataSymbols.Add(disabled);
        var noListing = new Asset { Name = "E no listing", Type = AssetType.Stock, Isin = "NOLISTING" };
        context.Assets.AddRange(healthy, missing, stale, failing, noListing);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var listing = missing.Listings.Single();
        context.InvestmentTransactions.AddRange(
            Trade(listing.Id, 10, InvestmentTransactionType.Buy, 5),
            Trade(listing.Id, 10, InvestmentTransactionType.Sell, 2),
            Trade(listing.Id, 11, InvestmentTransactionType.Buy, 1),
            Trade(listing.Id, 11, InvestmentTransactionType.Sell, 1),
            Trade(listing.Id, 12, InvestmentTransactionType.Buy, 2));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new AssetRepository(context);
        var first = await repository.GetIncomplete(1, 2, null, _cutoff, TestContext.Current.CancellationToken);
        Assert.Equal(4, first.TotalCount);
        Assert.Equal(new[] { "B missing", "C stale" }, first.Items.Select(x => x.Name));
        Assert.Equal(2, first.Items[0].Holders);
        Assert.Contains("Missing venue", first.Items[0].Reasons);
        Assert.Contains("Missing trading currency", first.Items[0].Reasons);
        Assert.True(first.Items[0].MissingIdentity);
        Assert.True(first.Items[0].MissingType);
        Assert.True(first.Items[1].StalePrice);
        var second = await repository.GetIncomplete(2, 2, null, _cutoff, TestContext.Current.CancellationToken);
        Assert.True(second.Items[0].AllSymbolsFailing);
        Assert.Equal("request failed", second.Items[0].LastError);
        Assert.Null(second.Items[1].ListingId);
        var search = await repository.GetIncomplete(1, 20, "stale", _cutoff, TestContext.Current.CancellationToken);
        Assert.Equal("C stale", Assert.Single(search.Items).Name);
        listing.ExchangeMic = "XWAR";
        listing.TradingCurrency = "PLN";
        missing.Type = AssetType.Stock;
        missing.Isin = "FIXED";
        listing.MarketDataSymbols.Add(Symbol(_cutoff, null, MarketDataProvider.Eodhd));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Empty((await repository.GetIncomplete(1, 20, "missing", _cutoff, TestContext.Current.CancellationToken)).Items);
    }

    private static Asset Asset(string name, string identity) => new()
    {
        Name = name,
        Isin = identity,
        Type = AssetType.Stock,
        Listings = [new() { Ticker = identity, ExchangeMic = "XWAR", TradingCurrency = "PLN" }]
    };
    private static MarketDataSymbol Symbol(DateTimeOffset? success, string? error, MarketDataProvider provider = MarketDataProvider.AlphaVantage) => new()
    {
        Provider = provider,
        Symbol = Guid.NewGuid().ToString(),
        LastSuccessfulPriceFetchAt = success,
        LastError = error
    };
    private static InvestmentTransaction Trade(long listingId, long userId, InvestmentTransactionType type, decimal quantity) => new()
    {
        AssetListingId = listingId,
        UserId = userId,
        AccountId = 1,
        Type = type,
        Quantity = quantity,
        Currency = "PLN",
        TradeDate = new(2026, 10, 29)
    };

    // SQLite needs a numeric timestamp representation; the production providers support DateTimeOffset.
    private sealed class QueueContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var converter = new ValueConverter<DateTimeOffset, long>(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties())
                .Where(x => x.ClrType == typeof(DateTimeOffset) || x.ClrType == typeof(DateTimeOffset?)))
                property.SetValueConverter(converter);
        }
    }
}