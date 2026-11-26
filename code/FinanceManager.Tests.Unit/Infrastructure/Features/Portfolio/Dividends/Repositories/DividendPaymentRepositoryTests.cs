using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Portfolio.Dividends.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Infrastructure.Features.Portfolio.Dividends.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Portfolio.Dividends.Repositories;

[Trait("Category", "Unit")]
public class DividendPaymentRepositoryTests
{
    [Fact]
    public async Task OwnerWideQueriesReadOneOwnerOnRelationalProvider()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var context = new LedgerContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync(ct);
        context.Assets.Add(new Asset
        {
            Id = 1,
            Name = "Fund",
            Type = AssetType.ETF,
            Listings =
            [
                new() { Id = 8, Ticker = "FUND", ExchangeMic = "XNAS", TradingCurrency = "USD" },
                new() { Id = 9, Ticker = "PAID", ExchangeMic = "XNAS", TradingCurrency = "USD" },
                new() { Id = 10, Ticker = "NONE", ExchangeMic = "XNAS", TradingCurrency = "USD" }
            ]
        });
        context.InvestmentTransactions.AddRange(
            new InvestmentTransaction { UserId = 1, AssetListingId = 8, Quantity = 1, Currency = "USD" },
            new InvestmentTransaction { UserId = 2, AssetListingId = 10, Quantity = 1, Currency = "USD" });
        context.DividendPayments.AddRange(
            Payment(1, 1, new(2020, 1, 1)), Payment(2, 1, new(2020, 1, 1)),
            Payment(3, 1, new(2019, 1, 1)), Payment(4, 2, new(2021, 1, 1)));
        context.DividendPayments.Add(new DividendPayment { Id = 5, UserId = 1, AssetListingId = 9, PaymentDate = new(2018, 1, 1), Amount = 5, Currency = "USD" });
        await context.SaveChangesAsync(ct);
        var repository = new DividendPaymentRepository(context);

        var first = await repository.GetHistory(1, 1, 2, ct);
        var second = await repository.GetHistory(1, 2, 2, ct);

        Assert.Equal(4, first.TotalCount);
        Assert.Equal(new long[] { 2, 1, 3, 5 }, first.Items.Concat(second.Items).Select(x => x.Id));
        Assert.Equal(new long[] { 8, 9 }, (await repository.GetHoldings(1, ct)).Select(x => x.AssetListingId));
        Assert.True(await repository.HasHolding(1, 8, ct));
        Assert.True(await repository.HasHolding(1, 9, ct));
        Assert.False(await repository.HasHolding(1, 10, ct));
        Assert.False(await repository.HasHolding(2, 9, ct));
        Assert.True(await repository.HasHolding(2, 10, ct));
    }

    private static DividendPayment Payment(long id, int user, DateOnly date) => new()
    { Id = id, UserId = user, AssetListingId = 8, PaymentDate = date, Amount = 5, Currency = "USD" };

    // SQLite requires a numeric timestamp representation; production providers support DateTimeOffset.
    private sealed class LedgerContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            var converter = new ValueConverter<DateTimeOffset, long>(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            foreach (var property in builder.Model.GetEntityTypes().SelectMany(x => x.GetProperties())
                .Where(x => x.ClrType == typeof(DateTimeOffset) || x.ClrType == typeof(DateTimeOffset?))) property.SetValueConverter(converter);
        }
    }
}