using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public sealed class SqliteCurrencyImportBatchTests : IDisposable
{
    private const int _accountId = 7411;

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly CurrencyEntryRepository _repo;

    // Ordered SQLite setup requires explicit constructor statements and cannot be expressed with a primary constructor.
    public SqliteCurrencyImportBatchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options);

        _context.Database.EnsureCreated();
        _repo = new CurrencyEntryRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddBatch_PersistsAllEntries_AndPopulatesGeneratedIds()
    {
        var ct = TestContext.Current.CancellationToken;
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var entry1 = new CurrencyAccountEntry(_accountId, 0, jan10, 0, 500m);
        var entry2 = new CurrencyAccountEntry(_accountId, 0, jan15, 0, 300m);

        var added = await _repo.Add([entry2, entry1], recalculate: false, cancellationToken: ct);

        Assert.True(added);
        Assert.True(entry1.EntryId > 0);
        Assert.True(entry2.EntryId > 0);
        Assert.NotEqual(entry1.EntryId, entry2.EntryId);

        var dbEntries = await _context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.AccountId == _accountId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        Assert.Equal(2, dbEntries.Count);
        Assert.Equal(entry1.EntryId, dbEntries[0].EntryId);
        Assert.Equal(entry2.EntryId, dbEntries[1].EntryId);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AddBatch_WithOutOfOrderDates_AnchorsRecalculationAtEarliestEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);
        var jan25 = new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc);

        await _repo.Add(new CurrencyAccountEntry(_accountId, 0, jan10, 0, 1_000m), recalculate: true, cancellationToken: ct);
        await _repo.Add(new CurrencyAccountEntry(_accountId, 0, jan20, 0, 500m), recalculate: true, cancellationToken: ct);

        var entryJan25 = new CurrencyAccountEntry(_accountId, 0, jan25, 0, 300m);
        var entryJan15 = new CurrencyAccountEntry(_accountId, 0, jan15, 0, 200m);

        var added = await _repo.Add([entryJan25, entryJan15], recalculate: true, cancellationToken: ct);

        Assert.True(added);
        Assert.True(entryJan25.EntryId > 0);
        Assert.True(entryJan15.EntryId > 0);

        var entries = await _context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.AccountId == _accountId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        Assert.Equal(4, entries.Count);
        Assert.Equal(1_000m, entries[0].Value);
        Assert.Equal(1_200m, entries[1].Value);
        Assert.Equal(1_700m, entries[2].Value);
        Assert.Equal(2_000m, entries[3].Value);
    }

    [Fact]
    public async Task AddBatch_WithTrackedLabels_ReusesExistingLabelsWithoutDuplicateInserts()
    {
        var ct = TestContext.Current.CancellationToken;
        var existingLabel = new FinancialLabel { Name = "Imported" };
        _context.FinancialLabels.Add(existingLabel);
        await _context.SaveChangesAsync(ct);

        var postingDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var entry = new CurrencyAccountEntry(_accountId, 0, postingDate, 0, 500m)
        {
            Labels = [new FinancialLabel { Id = existingLabel.Id, Name = existingLabel.Name }]
        };

        var added = await _repo.Add([entry], recalculate: false, cancellationToken: ct);

        Assert.True(added);
        Assert.Equal(1, await _context.FinancialLabels.CountAsync(ct));

        var loaded = await _context.CurrencyEntries
            .Include(e => e.Labels)
            .SingleAsync(e => e.EntryId == entry.EntryId, ct);

        Assert.Single(loaded.Labels);
        Assert.Equal(existingLabel.Id, loaded.Labels.First().Id);
    }
}