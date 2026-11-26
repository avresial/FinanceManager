using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public sealed class SqliteCurrencyImportBatchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly CashEntryRepository _repo;

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
        _repo = new CashEntryRepository(_context);
        _context.SaveChanges();
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

        var entry1 = new CashEntry(0, jan10, 500m) { UserId = 1 };
        var entry2 = new CashEntry(0, jan15, 300m) { UserId = 1 };

        var added = await _repo.Add([entry2, entry1], cancellationToken: ct);

        Assert.True(added);
        Assert.True(entry1.EntryId > 0);
        Assert.True(entry2.EntryId > 0);
        Assert.NotEqual(entry1.EntryId, entry2.EntryId);

        var dbEntries = await _context.CurrencyEntries
            .AsNoTracking()
            .Where(e => e.UserId == 1)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        Assert.Equal(2, dbEntries.Count);
        Assert.Equal(entry1.EntryId, dbEntries[0].EntryId);
        Assert.Equal(entry2.EntryId, dbEntries[1].EntryId);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AddBatch_WithTrackedLabels_ReusesExistingLabelsWithoutDuplicateInserts()
    {
        var ct = TestContext.Current.CancellationToken;
        var existingLabel = new FinancialLabel { Name = "Imported" };
        _context.FinancialLabels.Add(existingLabel);
        await _context.SaveChangesAsync(ct);

        var postingDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var entry = new CashEntry(0, postingDate, 500m)
        {
            UserId = 1,
            Labels = [new FinancialLabel { Id = existingLabel.Id, Name = existingLabel.Name }]
        };

        var added = await _repo.Add([entry], cancellationToken: ct);

        Assert.True(added);
        Assert.Equal(1, await _context.FinancialLabels.CountAsync(ct));

        var loaded = await _context.CurrencyEntries
            .Include(e => e.Labels)
            .SingleAsync(e => e.EntryId == entry.EntryId, ct);

        Assert.Single(loaded.Labels);
        Assert.Equal(existingLabel.Id, loaded.Labels.First().Id);
    }
}