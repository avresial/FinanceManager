using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// Covers the backfill-facing exchange-rate repository methods (issue #597): latest-date lookup,
/// existing-date filtering and idempotent bulk insert. Uses the EF Core in-memory provider (which
/// ignores unique indexes, so cross-instance duplicate rejection is left to the real database; here
/// idempotency is asserted through the repository's own filter-before-insert path).
/// </summary>
[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class ExchangeRateRepositoryBackfillTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly DateTime _d1 = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _d2 = new(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _d3 = new(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AddRange_InsertsOnlyMissingDates()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var repo = new ExchangeRateRepository(context);

        await repo.Add("USD", "PLN", _d2, 4.0m, ct);

        var inserted = await repo.AddRange("USD", "PLN",
            [(_d1, 3.9m), (_d2, 4.0m), (_d3, 4.1m)], ct);

        Assert.Equal(2, inserted);
        var dates = (await repo.GetExistingDates("USD", "PLN", _d1, _d3, ct)).Select(d => d.Date).OrderBy(d => d).ToList();
        Assert.Equal([_d1.Date, _d2.Date, _d3.Date], dates);
    }

    [Fact]
    public async Task AddRange_RunAgain_IsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var repo = new ExchangeRateRepository(context);

        var rates = new[] { (_d1, 3.9m), (_d2, 4.0m), (_d3, 4.1m) };

        var first = await repo.AddRange("USD", "PLN", rates, ct);
        var second = await repo.AddRange("USD", "PLN", rates, ct);

        Assert.Equal(3, first);
        Assert.Equal(0, second);
        Assert.Equal(3, await context.ExchangeRates.CountAsync(ct));
    }

    [Fact]
    public async Task GetLatestDate_ReturnsNewestStoredDate_OrNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var repo = new ExchangeRateRepository(context);

        Assert.Null(await repo.GetLatestDate("USD", "PLN", ct));

        await repo.AddRange("USD", "PLN", [(_d1, 3.9m), (_d3, 4.1m), (_d2, 4.0m)], ct);

        Assert.Equal(_d3.Date, await repo.GetLatestDate("USD", "PLN", ct));
    }
}