using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class StockPriceRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Currency MakeCurrency(int id = 1) =>
        new(id, "USD", "$");

    private static StockDetails MakeStockDetails(string isin, Currency currency) =>
        new() { Isin = isin, Ticker = isin, Currency = currency };

    private static StockPriceDto MakePriceDto(StockDetails details, DateTime date, decimal price = 100m) =>
        new(0, details, price, date.Date);

    // -------------------------------------------------------------------------
    // Get — date-boundary semantics
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsPrice_WhenDateMatches()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 1, 15), 150m));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var result = await repo.Get("US0378331005", new DateTime(2025, 1, 15));

        Assert.NotNull(result);
        Assert.Equal(150m, result.PricePerUnit);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenDateDoesNotMatch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 1, 15)));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var result = await repo.Get("US0378331005", new DateTime(2025, 1, 16));

        Assert.Null(result);
    }

    [Fact]
    public async Task Get_IgnoresTimeComponent_WhenDateWithTimeIsProvided()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 3, 10)));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        // Querying with a time component should still find the midnight-stored price
        var result = await repo.Get("US0378331005", new DateTime(2025, 3, 10, 14, 30, 0));

        Assert.NotNull(result);
    }

    // -------------------------------------------------------------------------
    // GetRange — boundary semantics (inclusive start, inclusive end)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetRange_IncludesStartDate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 1)));
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 2)));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var result = await repo.GetRange("US0378331005", new DateTime(2025, 6, 1), new DateTime(2025, 6, 1));

        Assert.Single(result);
        Assert.Equal(new DateTime(2025, 6, 1), result[0].Date);
    }

    [Fact]
    public async Task GetRange_IncludesEndDate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 5)));
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 6)));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var result = await repo.GetRange("US0378331005", new DateTime(2025, 6, 5), new DateTime(2025, 6, 6));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetRange_ExcludesPricesOutsideRange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 1)));
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 2)));
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 3)));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var result = await repo.GetRange("US0378331005", new DateTime(2025, 6, 2), new DateTime(2025, 6, 2));

        Assert.Single(result);
        Assert.Equal(new DateTime(2025, 6, 2), result[0].Date);
    }

    // -------------------------------------------------------------------------
    // Add(IEnumerable) — bulk-add diffing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddBulk_InsertsNewPrices()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var prices = new List<StockPrice>
        {
            new() { Isin = "US0378331005", PricePerUnit = 200m, Currency = currency, Date = new DateTime(2025, 6, 1) },
            new() { Isin = "US0378331005", PricePerUnit = 210m, Currency = currency, Date = new DateTime(2025, 6, 2) },
        };

        await repo.Add(prices);

        var stored = await ctx.StockPrices.ToListAsync(ct);
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task AddBulk_UpdatesExistingPrices()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 1), 100m));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var prices = new List<StockPrice>
        {
            new() { Isin = "US0378331005", PricePerUnit = 999m, Currency = currency, Date = new DateTime(2025, 6, 1) },
        };

        await repo.Add(prices);

        var stored = await ctx.StockPrices.ToListAsync(ct);
        Assert.Single(stored);
        Assert.Equal(999m, stored[0].PricePerUnit);
    }

    [Fact]
    public async Task AddBulk_MixedInsertAndUpdate_HandlesCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);
        ctx.StockPrices.Add(MakePriceDto(details, new DateTime(2025, 6, 1), 100m));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var prices = new List<StockPrice>
        {
            new() { Isin = "US0378331005", PricePerUnit = 150m, Currency = currency, Date = new DateTime(2025, 6, 1) }, // update
            new() { Isin = "US0378331005", PricePerUnit = 200m, Currency = currency, Date = new DateTime(2025, 6, 2) }, // insert
        };

        await repo.Add(prices);

        var stored = await ctx.StockPrices.OrderBy(x => x.Date).ToListAsync(ct);
        Assert.Equal(2, stored.Count);
        Assert.Equal(150m, stored[0].PricePerUnit);
        Assert.Equal(200m, stored[1].PricePerUnit);
    }

    [Fact]
    public async Task AddBulk_SkipsPriceWithUnknownIsin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var prices = new List<StockPrice>
        {
            new() { Isin = "UNKNOWN_ISIN", PricePerUnit = 100m, Currency = currency, Date = new DateTime(2025, 6, 1) },
        };

        await repo.Add(prices);

        var stored = await ctx.StockPrices.ToListAsync(ct);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task AddBulk_EmptyList_DoesNothing()
    {
        await using var ctx = CreateContext();
        var repo = new StockPriceRepository(ctx);

        await repo.Add([]);

        var stored = await ctx.StockPrices.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(stored);
    }

    // -------------------------------------------------------------------------
    // GetLatestMissing — gap detection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetLatestMissing_ReturnsToday_WhenNoPricesExist()
    {
        await using var ctx = CreateContext();
        var repo = new StockPriceRepository(ctx);

        var result = await repo.GetLatestMissing("US0378331005");

        Assert.Equal(DateTime.UtcNow.Date, result);
    }

    [Fact]
    public async Task GetLatestMissing_ReturnsNull_WhenNoContinuousGapExists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);

        var today = DateTime.UtcNow.Date;
        ctx.StockPrices.Add(MakePriceDto(details, today));
        ctx.StockPrices.Add(MakePriceDto(details, today.AddDays(-1)));
        ctx.StockPrices.Add(MakePriceDto(details, today.AddDays(-2)));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var result = await repo.GetLatestMissing("US0378331005");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestMissing_ReturnsGapDate_WhenDayIsMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var currency = MakeCurrency();
        ctx.Currencies.Add(currency);
        var details = MakeStockDetails("US0378331005", currency);
        ctx.StockDetails.Add(details);

        var today = DateTime.UtcNow.Date;
        // today and today-1 present, today-2 skipped, today-3 present → gap at today-2
        ctx.StockPrices.Add(MakePriceDto(details, today));
        ctx.StockPrices.Add(MakePriceDto(details, today.AddDays(-1)));
        ctx.StockPrices.Add(MakePriceDto(details, today.AddDays(-3)));
        await ctx.SaveChangesAsync(ct);

        var repo = new StockPriceRepository(ctx);
        var result = await repo.GetLatestMissing("US0378331005");

        Assert.Equal(today.AddDays(-2), result);
    }
}