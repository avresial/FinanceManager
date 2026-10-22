using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public class TransactionLogSearchTests
{
    private static readonly DateTime _day = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("InMemory", "Currency")]
    [InlineData("InMemory", "Bond")]
    [InlineData("InMemory", "Investment")]
    [InlineData("Sqlite", "Currency")]
    [InlineData("Sqlite", "Bond")]
    [InlineData("Sqlite", "Investment")]
    [InlineData("PostgreSql", "Currency")]
    [InlineData("PostgreSql", "Bond")]
    [InlineData("PostgreSql", "Investment")]
    [InlineData("SqlServer", "Currency")]
    [InlineData("SqlServer", "Bond")]
    [InlineData("SqlServer", "Investment")]
    public async Task SearchCombinesStreamTextAndAccountMatchesWithRangeScopeAndCursorBeforeTake(string provider, string stream)
    {
        await using var database = await SearchDatabase.Create(provider);
        var context = database.Context;
        if (stream == "Currency")
        {
            context.CurrencyEntries.AddRange(
                new CurrencyAccountEntry(1, 1, _day, 1, 1) { ContractorDetails = null },
                new CurrencyAccountEntry(2, 2, _day, 1, 1),
                new CurrencyAccountEntry(1, 3, _day, 1, 1) { ContractorDetails = "MiXeD shop" },
                new CurrencyAccountEntry(1, 4, _day, 1, 1) { Description = "MIXED purchase" },
                new CurrencyAccountEntry(1, 5, _day, 1, 1) { Description = "other" },
                new CurrencyAccountEntry(1, 6, _day.AddDays(1), 1, 1) { Description = "mixed" },
                new CurrencyAccountEntry(999, 7, _day, 1, 1) { Description = "mixed" });
        }
        else if (stream == "Bond")
        {
            context.Bonds.Add(new BondDetails("MiXeD Treasury", "Issuer", new(2026, 1, 1), new(2036, 1, 1), []) { Id = 1 });
            context.BondEntries.AddRange(
                new BondAccountEntry(1, 1, _day, 1, 1, 99), new BondAccountEntry(2, 2, _day, 1, 1, 99),
                new BondAccountEntry(1, 3, _day, 1, 1, 1), new BondAccountEntry(1, 4, _day, 1, 1, 1),
                new BondAccountEntry(1, 5, _day, 1, 1, 99), new BondAccountEntry(1, 6, _day.AddDays(1), 1, 1, 1),
                new BondAccountEntry(999, 7, _day, 1, 1, 1));
        }
        else
        {
            context.AssetListings.AddRange(Listing(1, "MiXeD", "Other"), Listing(2, "OTHER", "MiXeD instrument"), Listing(3, "NONE", "Other"));
            context.InvestmentTransactions.AddRange(Trade(1, 1, 3), Trade(2, 2, 3), Trade(3, 1, 1), Trade(4, 1, 2), Trade(5, 1, 3), Trade(6, 1, 1, 1), Trade(7, 999, 1));
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        Assert.Equal(new long[] { 4L, 3L }, await Page(context, stream, 2, null, " mIxEd ", [2, 999]));
        Assert.Equal(new long[] { 2L }, await Page(context, stream, 2, 3, "mixed", [2, 999]));
        Assert.Empty(await Page(context, stream, 2, null, "absent", []));
        Assert.Equal(new long[] { 5L, 4L, 3L, 2L, 1L }, await Page(context, stream, 10, null, "  ", []));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("InMemory")]
    [InlineData("Sqlite")]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task SearchTreatsWildcardCharactersLiterally(string provider)
    {
        await using var database = await SearchDatabase.Create(provider);
        var context = database.Context;
        context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(1, 1, _day, 1, 1) { Description = "100%_sale[one]" },
            new CurrencyAccountEntry(1, 2, _day, 1, 1) { Description = "100xxsaleo" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 1L }, await Page(context, "Currency", 10, null, "%_sale[one]", []));
    }

    private static async Task<long[]> Page(AppDbContext context, string stream, int take, int? cursor, string search, int[] matchedAccounts)
    {
        var ct = TestContext.Current.CancellationToken;
        var date = cursor.HasValue ? _day : (DateTime?)null;
        if (stream == "Currency")
            return [.. (await new CurrencyEntryRepository(context).GetHistoryPage([1, 2], _day, _day, take, date, cursor, ct, search, matchedAccounts)).Select(e => (long)e.EntryId)];
        if (stream == "Bond")
            return [.. (await new BondEntryRepository(context).GetHistoryPage([1, 2], _day, _day, take, date, cursor, ct, search, matchedAccounts)).Select(e => (long)e.EntryId)];
        var day = DateOnly.FromDateTime(_day);
        return [.. (await new InvestmentTransactionRepository(context).GetHistoryPage([1, 2], day, day, take, cursor.HasValue ? day : null, cursor, ct, search, matchedAccounts)).Select(e => e.Id)];
    }

    private static AssetListing Listing(long id, string ticker, string name) => new()
    {
        Id = id,
        Ticker = ticker,
        Asset = new Asset { Id = id, Name = name },
        ExchangeMic = "XWAR",
        ExchangeName = "Warsaw",
        TradingCurrency = "PLN"
    };

    private static InvestmentTransaction Trade(long id, int accountId, long listingId, int offset = 0) => new()
    {
        Id = id,
        AccountId = accountId,
        UserId = 1,
        AssetListingId = listingId,
        TradeDate = DateOnly.FromDateTime(_day).AddDays(offset),
        Type = InvestmentTransactionType.Buy,
        Quantity = 1,
        UnitPrice = 1,
        Currency = "PLN"
    };

    // Stable explicit IDs make the same ordering assertions usable on every provider.
    private sealed class SearchContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<CurrencyAccountEntry>().Property(e => e.EntryId).ValueGeneratedNever();
            modelBuilder.Entity<BondAccountEntry>().Property(e => e.EntryId).ValueGeneratedNever();
            modelBuilder.Entity<BondDetails>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<Asset>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<AssetListing>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<InvestmentTransaction>().Property(e => e.Id).ValueGeneratedNever();
        }
    }

    // Relational cases own disposable databases. Foreign keys are disabled only in this fixture
    // so unrelated account/identity graphs do not obscure the repository query contract.
    private sealed class SearchDatabase(AppDbContext context, bool deleteDatabase) : IAsyncDisposable
    {
        public AppDbContext Context { get; } = context;

        public static async Task<SearchDatabase> Create(string provider)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>();
            if (provider == "InMemory") options.UseInMemoryDatabase(Guid.NewGuid().ToString());
            else if (provider == "Sqlite") options.UseSqlite("Data Source=:memory:;Foreign Keys=False");
            else
            {
                var connection = Environment.GetEnvironmentVariable($"FM_SEARCH_{provider.ToUpperInvariant()}_CONNECTION");
                Assert.SkipUnless(!string.IsNullOrWhiteSpace(connection), $"Set FM_SEARCH_{provider.ToUpperInvariant()}_CONNECTION for disposable {provider} tests.");
                var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connection };
                builder["Database"] = $"fm_search_{Guid.NewGuid():N}";
                if (provider == "PostgreSql") options.UseNpgsql(builder.ConnectionString);
                else options.UseSqlServer(builder.ConnectionString);
            }
            var context = new SearchContext(options.Options);
            if (provider == "Sqlite") await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            if (provider == "PostgreSql")
            {
                await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
                await context.Database.ExecuteSqlRawAsync("SET session_replication_role = replica", TestContext.Current.CancellationToken);
            }
            if (provider == "SqlServer")
                await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'", TestContext.Current.CancellationToken);
            return new SearchDatabase(context, provider is "PostgreSql" or "SqlServer");
        }

        public async ValueTask DisposeAsync()
        {
            if (deleteDatabase) await Context.Database.EnsureDeletedAsync(CancellationToken.None);
            await Context.DisposeAsync();
        }
    }
}