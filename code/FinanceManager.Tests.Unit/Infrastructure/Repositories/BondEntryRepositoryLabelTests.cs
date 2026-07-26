using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class BondEntryRepositoryLabelTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task AddLabel_AttachesLabelToEntry()
    {
        await using var context = CreateContext();
        var entry = new BondAccountEntry(1, 0, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, 100, 5);
        context.BondEntries.Add(entry);
        var label = new FinancialLabel { Name = "Coupon" };
        context.FinancialLabels.Add(label);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new BondEntryRepository(context);

        var result = await repository.AddLabel(entry.EntryId, label.Id);

        Assert.True(result);
        var stored = await context.BondEntries.Include(e => e.Labels)
            .FirstAsync(e => e.EntryId == entry.EntryId, TestContext.Current.CancellationToken);
        Assert.Contains(stored.Labels, l => l.Id == label.Id);
    }

    [Fact]
    public async Task AddLabel_IsIdempotent()
    {
        await using var context = CreateContext();
        var entry = new BondAccountEntry(1, 0, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, 100, 5);
        context.BondEntries.Add(entry);
        var label = new FinancialLabel { Name = "Coupon" };
        context.FinancialLabels.Add(label);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new BondEntryRepository(context);
        await repository.AddLabel(entry.EntryId, label.Id);
        var result = await repository.AddLabel(entry.EntryId, label.Id);

        Assert.True(result);
        var stored = await context.BondEntries.Include(e => e.Labels)
            .FirstAsync(e => e.EntryId == entry.EntryId, TestContext.Current.CancellationToken);
        Assert.Single(stored.Labels);
    }

    [Fact]
    public async Task AddLabel_ReturnsFalse_WhenLabelMissing()
    {
        await using var context = CreateContext();
        var entry = new BondAccountEntry(1, 0, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, 100, 5);
        context.BondEntries.Add(entry);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new BondEntryRepository(context);

        var result = await repository.AddLabel(entry.EntryId, labelId: 999);

        Assert.False(result);
    }
}