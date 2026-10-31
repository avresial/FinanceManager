using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

[Trait("Category", "Unit")]
public class DividendPaymentRepositoryTests
{
    [Fact]
    public async Task RelationalLedgerPagesStableTiesIsolatesOwnersAndCascadesAccountDeletion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var context = new LedgerContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync(ct);
        context.Accounts.AddRange(new FinancialAccountBaseDto { AccountId = 4, UserId = 1, AccountType = AccountType.Stock }, new FinancialAccountBaseDto { AccountId = 5, UserId = 2, AccountType = AccountType.Stock });
        context.Assets.Add(new Asset { Id = 1, Name = "Fund", Type = AssetType.ETF, Listings = [new() { Id = 8, Ticker = "FUND", ExchangeMic = "XNAS", TradingCurrency = "USD" }] });
        context.InvestmentTransactions.Add(new InvestmentTransaction { UserId = 1, AccountId = 4, AssetListingId = 8, Quantity = 1, Currency = "USD", TradeDate = new(2019, 1, 1) });
        context.DividendPayments.AddRange(
            Payment(1, 1, 4, new(2020, 1, 1)), Payment(2, 1, 4, new(2020, 1, 1)),
            Payment(3, 1, 4, new(2019, 1, 1)), Payment(4, 2, 5, new(2021, 1, 1)));
        await context.SaveChangesAsync(ct);
        var repository = new DividendPaymentRepository(context);
        var first = await repository.GetHistory(1, 4, 1, 2, ct);
        var second = await repository.GetHistory(1, 4, 2, 2, ct);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(new long[] { 2, 1, 3 }, first.Items.Concat(second.Items).Select(x => x.Id));
        Assert.True(await repository.HasHolding(1, 4, 8, ct));
        Assert.False(await repository.HasHolding(2, 4, 8, ct));
        Assert.Single(await repository.GetHoldings(1, 4, ct));
        Assert.Empty(await repository.GetHoldings(2, 4, ct));
        await context.Accounts.Where(x => x.AccountId == 4).ExecuteDeleteAsync(ct);
        Assert.Equal(4, (await context.DividendPayments.AsNoTracking().SingleAsync(ct)).Id);
        Assert.Single(await context.InvestmentTransactions.ToListAsync(ct));
    }
    private static DividendPayment Payment(long id, int user, int account, DateOnly date) => new()
    { Id = id, UserId = user, AccountId = account, AssetListingId = 8, PaymentDate = date, Amount = 5, Currency = "USD" };

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