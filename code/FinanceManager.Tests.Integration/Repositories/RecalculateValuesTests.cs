using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// Asserts that running-balance recalculation produces correct values after out-of-order
/// inserts, updates, and deletes. All tests run against the EF Core InMemory provider
/// (the C# fallback path). The relational set-based SQL path is structurally equivalent
/// and is validated by manual/SQL-level testing against the target database.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RecalculateValuesTests : IDisposable
{
    private const int _accountId = 9001;

    private readonly AppDbContext _context;

    public RecalculateValuesTests()
    {
        _context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ── Currency ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Currency_OutOfOrderInsert_RecalculatesAllSubsequentBalances()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new CurrencyEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan10, 0, 1000m), recalculate: true, cancellationToken: ct);
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan20, 0, 500m), recalculate: true, cancellationToken: ct);

        // Insert between existing entries — triggers recalculation from jan15 onwards.
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan15, 0, 200m), recalculate: true, cancellationToken: ct);

        var entries = await _context.CurrencyEntries
            .Where(e => e.AccountId == _accountId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        Assert.Equal(3, entries.Count);
        Assert.Equal(1000m, entries[0].Value); // jan10
        Assert.Equal(1200m, entries[1].Value); // jan15: 1000 + 200
        Assert.Equal(1700m, entries[2].Value); // jan20: 1200 + 500
    }

    [Fact]
    public async Task Currency_RecalculateWholeAccount_RebuildsDriftedBalances()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new CurrencyEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        // Simulate legacy import drift: each entry's Value equals its ValueChange and no recalc ran.
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan10, 1000m, 1000m), recalculate: false, cancellationToken: ct);
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan15, 200m, 200m), recalculate: false, cancellationToken: ct);
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan20, 500m, 500m), recalculate: false, cancellationToken: ct);

        await repo.RecalculateValues(_accountId, ct);

        var entries = await _context.CurrencyEntries
            .Where(e => e.AccountId == _accountId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        Assert.Equal(3, entries.Count);
        Assert.Equal(1000m, entries[0].Value); // jan10
        Assert.Equal(1200m, entries[1].Value); // jan15: 1000 + 200
        Assert.Equal(1700m, entries[2].Value); // jan20: 1200 + 500
    }

    [Fact]
    public async Task Currency_Update_RecalculatesFromUpdatedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new CurrencyEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan10, 0, 1000m), recalculate: true, cancellationToken: ct);
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan20, 0, -200m), recalculate: true, cancellationToken: ct);

        var entry = await _context.CurrencyEntries.FirstAsync(e => e.PostingDate == jan10, ct);
        await repo.Update(new CurrencyAccountEntry(_accountId, entry.EntryId, jan10, 0, 1500m));

        var entries = await _context.CurrencyEntries
            .Where(e => e.AccountId == _accountId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        Assert.Equal(1500m, entries[0].Value); // jan10 updated
        Assert.Equal(1300m, entries[1].Value); // jan20: 1500 - 200
    }

    [Fact]
    public async Task Currency_Delete_RecalculatesFromNextEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new CurrencyEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan10, 0, 1000m), recalculate: true, cancellationToken: ct);
        await repo.Add(new CurrencyAccountEntry(_accountId, 0, jan20, 0, 500m), recalculate: true, cancellationToken: ct);

        var entry = await _context.CurrencyEntries.FirstAsync(e => e.PostingDate == jan10, ct);
        await repo.Delete(_accountId, entry.EntryId, ct);

        var remaining = await _context.CurrencyEntries
            .Where(e => e.AccountId == _accountId)
            .ToListAsync(ct);

        Assert.Single(remaining);
        Assert.Equal(500m, remaining[0].Value); // no prior entry → value = valueChange
    }

    // ── Bond ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bond_OutOfOrderInsert_RecalculatesPerBondDetailsId()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new BondEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        const int bondA = 1;
        const int bondB = 2;

        await repo.Add(new BondAccountEntry(_accountId, 0, jan10, 0, 500m, bondA), recalculate: true, cancellationToken: ct);
        await repo.Add(new BondAccountEntry(_accountId, 0, jan20, 0, 300m, bondA), recalculate: true, cancellationToken: ct);
        // Different bond — must remain independent.
        await repo.Add(new BondAccountEntry(_accountId, 0, jan10, 0, 1000m, bondB), recalculate: true, cancellationToken: ct);

        // Out-of-order insert for bondA.
        await repo.Add(new BondAccountEntry(_accountId, 0, jan15, 0, 100m, bondA), recalculate: true, cancellationToken: ct);

        var bondAEntries = await _context.BondEntries
            .Where(e => e.AccountId == _accountId && e.BondDetailsId == bondA)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        var bondBEntries = await _context.BondEntries
            .Where(e => e.AccountId == _accountId && e.BondDetailsId == bondB)
            .ToListAsync(ct);

        Assert.Equal(3, bondAEntries.Count);
        Assert.Equal(500m, bondAEntries[0].Value);  // jan10
        Assert.Equal(600m, bondAEntries[1].Value);  // jan15: 500 + 100
        Assert.Equal(900m, bondAEntries[2].Value);  // jan20: 600 + 300

        Assert.Single(bondBEntries);
        Assert.Equal(1000m, bondBEntries[0].Value); // unaffected
    }

    [Fact]
    public async Task Bond_RecalculateWholeAccount_RebuildsDriftedBalancesPerBondDetailsId()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new BondEntryRepository(_context);
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        const int bondA = 1;
        const int bondB = 2;

        // Simulate legacy import drift: Value equals ValueChange across two bonds, no recalc.
        await repo.Add(new BondAccountEntry(_accountId, 0, jan10, 500m, 500m, bondA), recalculate: false, cancellationToken: ct);
        await repo.Add(new BondAccountEntry(_accountId, 0, jan20, 300m, 300m, bondA), recalculate: false, cancellationToken: ct);
        await repo.Add(new BondAccountEntry(_accountId, 0, jan10, 1000m, 1000m, bondB), recalculate: false, cancellationToken: ct);

        await repo.RecalculateValues(_accountId, ct);

        var bondAEntries = await _context.BondEntries
            .Where(e => e.AccountId == _accountId && e.BondDetailsId == bondA)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        var bondBEntries = await _context.BondEntries
            .Where(e => e.AccountId == _accountId && e.BondDetailsId == bondB)
            .ToListAsync(ct);

        Assert.Equal(500m, bondAEntries[0].Value); // jan10
        Assert.Equal(800m, bondAEntries[1].Value); // jan20: 500 + 300
        Assert.Single(bondBEntries);
        Assert.Equal(1000m, bondBEntries[0].Value); // single entry → value = valueChange
    }
}