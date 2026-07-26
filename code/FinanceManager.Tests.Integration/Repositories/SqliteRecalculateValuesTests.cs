using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// Covers running-balance recalculation on a <em>relational</em> provider, exercising the hand-written
/// set-based <c>UPDATE</c> rather than the managed row-by-row fallback that
/// <see cref="RecalculateValuesTests"/> covers.
/// </summary>
/// <remarks>
/// The windowed UPDATE has a different spelling per provider and had no automated coverage at all: the
/// InMemory provider takes the managed path, so a broken dialect could only be found by running against
/// a real database. SQLite is the one relational provider that runs in-process with no external server,
/// so these tests can assert the set-based SQL actually produces correct balances. The SQL Server and
/// PostgreSQL branches remain structurally equivalent statements validated against those databases.
///
/// Amounts are whole numbers on purpose. SQLite has no decimal type, so EF Core stores money as TEXT and
/// <c>SUM</c> coerces it to floating point; integral values keep the assertions about ordering and
/// anchoring free of float-representation noise, which is what these tests are actually about.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class SqliteRecalculateValuesTests : IDisposable
{
    private const int _accountId = 9101;

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public SqliteRecalculateValuesTests()
    {
        // A shared-cache in-memory database lives exactly as long as this connection, which keeps each
        // test isolated without touching the filesystem.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options);

        // Migrations in this repo are generated for SQL Server, so the schema is created from the model.
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Currency_SetBasedRecalculation_ProducesRunningBalances()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new CurrencyEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan10, 1000m, 1000m), recalculate: false);
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan15, 200m, 200m), recalculate: false);
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan20, 500m, 500m), recalculate: false);

        await repo.RecalculateValues(_accountId);

        var entries = await ReadCurrencyEntries(ct);

        Assert.Equal(3, entries.Count);
        Assert.Equal(1000m, entries[0].Value); // jan10
        Assert.Equal(1200m, entries[1].Value); // jan15: 1000 + 200
        Assert.Equal(1700m, entries[2].Value); // jan20: 1200 + 500
    }

    [Fact]
    public async Task Currency_OutOfOrderInsert_RecalculatesFromAnchor()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new CurrencyEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan10, 0, 1000m), recalculate: true);
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan20, 0, 500m), recalculate: true);

        // Inserting between existing entries recalculates from jan15 on, seeded by the jan10 anchor —
        // the part of the statement that reads a value from outside the updated range.
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan15, 0, 200m), recalculate: true);

        var entries = await ReadCurrencyEntries(ct);

        Assert.Equal(3, entries.Count);
        Assert.Equal(1000m, entries[0].Value); // jan10
        Assert.Equal(1200m, entries[1].Value); // jan15: anchored on jan10
        Assert.Equal(1700m, entries[2].Value); // jan20: 1200 + 500
    }

    [Fact]
    public async Task Bond_SetBasedRecalculation_PartitionsByBondDetailsId()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new BondEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        const int bondA = 1;
        const int bondB = 2;

        await repo.Add(new BondAccountEntry(_accountId, 0, jan10, 500m, 500m, bondA), recalculate: false);
        await repo.Add(new BondAccountEntry(_accountId, 0, jan15, 100m, 100m, bondA), recalculate: false);
        await repo.Add(new BondAccountEntry(_accountId, 0, jan20, 300m, 300m, bondA), recalculate: false);
        // A second bond in the same account must keep its own running total.
        await repo.Add(new BondAccountEntry(_accountId, 0, jan10, 1000m, 1000m, bondB), recalculate: false);

        await repo.RecalculateValues(_accountId);

        var bondAEntries = await ReadBondEntries(bondA, ct);
        var bondBEntries = await ReadBondEntries(bondB, ct);

        Assert.Equal(3, bondAEntries.Count);
        Assert.Equal(500m, bondAEntries[0].Value); // jan10
        Assert.Equal(600m, bondAEntries[1].Value); // jan15: 500 + 100
        Assert.Equal(900m, bondAEntries[2].Value); // jan20: 600 + 300

        Assert.Single(bondBEntries);
        Assert.Equal(1000m, bondBEntries[0].Value);
    }

    [Fact]
    public async Task Bond_OutOfOrderInsert_AnchorsPerBondDetailsId()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new BondEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        const int bondA = 1;
        const int bondB = 2;

        await repo.Add(new BondAccountEntry(_accountId, 0, jan10, 0, 500m, bondA), recalculate: true);
        await repo.Add(new BondAccountEntry(_accountId, 0, jan20, 0, 300m, bondA), recalculate: true);
        await repo.Add(new BondAccountEntry(_accountId, 0, jan10, 0, 1000m, bondB), recalculate: true);

        // Exercises the per-bond anchor selection: bondA anchors on its own jan10 row, not bondB's.
        await repo.Add(new BondAccountEntry(_accountId, 0, jan15, 0, 100m, bondA), recalculate: true);

        var bondAEntries = await ReadBondEntries(bondA, ct);
        var bondBEntries = await ReadBondEntries(bondB, ct);

        Assert.Equal(3, bondAEntries.Count);
        Assert.Equal(500m, bondAEntries[0].Value); // jan10
        Assert.Equal(600m, bondAEntries[1].Value); // jan15: 500 + 100
        Assert.Equal(900m, bondAEntries[2].Value); // jan20: 600 + 300

        Assert.Single(bondBEntries);
        Assert.Equal(1000m, bondBEntries[0].Value);
    }

    /// <summary>
    /// Guards the dialect switch itself: this fixture must be on the SQLite branch, not silently taking
    /// the managed fallback that would make every assertion above pass without testing any SQL.
    /// </summary>
    [Fact]
    public void Fixture_UsesRelationalSqliteProvider()
    {
        Assert.True(_context.Database.IsRelational());
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", _context.Database.ProviderName);
    }

    private async Task<List<CurrencyAccountEntry>> ReadCurrencyEntries(CancellationToken ct) =>
        await _context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.AccountId == _accountId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

    private async Task<List<BondAccountEntry>> ReadBondEntries(int bondDetailsId, CancellationToken ct) =>
        await _context.BondEntries
            .AsNoTracking()
            .Where(e => e.AccountId == _accountId && e.BondDetailsId == bondDetailsId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);
}