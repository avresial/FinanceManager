using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Bond.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class BondEntryRepositoryLabelTests
{
    private const int _userId = 7;

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BondAccountEntry Entry(int? accountId, int userId) =>
        new(accountId, 0, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, 100, 5) { UserId = userId };

    [Theory]
    [InlineData(1)]
    [InlineData(null)]
    public async Task AddLabels_AttachesLabelToTheOwnersEntry_LinkedOrNot(int? accountId)
    {
        await using var context = CreateContext();
        var entry = Entry(accountId, _userId);
        context.BondEntries.Add(entry);
        var label = new FinancialLabel { Name = "Coupon" };
        context.FinancialLabels.Add(label);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var added = await new BondEntryRepository(context).AddLabels(_userId, [(entry.EntryId, label.Id)], TestContext.Current.CancellationToken);

        Assert.Equal(1, added);
        var stored = await context.BondEntries.Include(e => e.Labels)
            .FirstAsync(e => e.EntryId == entry.EntryId, TestContext.Current.CancellationToken);
        Assert.Contains(stored.Labels, l => l.Id == label.Id);
    }

    [Fact]
    public async Task AddLabels_IsIdempotent()
    {
        await using var context = CreateContext();
        var entry = Entry(1, _userId);
        context.BondEntries.Add(entry);
        var label = new FinancialLabel { Name = "Coupon" };
        context.FinancialLabels.Add(label);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new BondEntryRepository(context);
        await repository.AddLabels(_userId, [(entry.EntryId, label.Id)], TestContext.Current.CancellationToken);
        var added = await repository.AddLabels(_userId, [(entry.EntryId, label.Id)], TestContext.Current.CancellationToken);

        Assert.Equal(0, added);
        var stored = await context.BondEntries.Include(e => e.Labels)
            .FirstAsync(e => e.EntryId == entry.EntryId, TestContext.Current.CancellationToken);
        Assert.Single(stored.Labels);
    }

    [Fact]
    public async Task AddLabels_IgnoresMissingLabelsForeignEntriesAndAZeroOwner()
    {
        await using var context = CreateContext();
        var own = Entry(1, _userId);
        var foreign = Entry(2, 99);
        context.BondEntries.AddRange(own, foreign);
        var label = new FinancialLabel { Name = "Coupon" };
        context.FinancialLabels.Add(label);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new BondEntryRepository(context);

        Assert.Equal(0, await repository.AddLabels(_userId, [(own.EntryId, 999)], TestContext.Current.CancellationToken));
        Assert.Equal(0, await repository.AddLabels(_userId, [(foreign.EntryId, label.Id)], TestContext.Current.CancellationToken));
        Assert.Equal(0, await repository.AddLabels(0, [(own.EntryId, label.Id)], TestContext.Current.CancellationToken));
        Assert.Empty((await context.BondEntries.Include(e => e.Labels).ToListAsync(TestContext.Current.CancellationToken)).SelectMany(e => e.Labels));
    }

    [Fact]
    public async Task GetByIds_ReturnsOnlyTheOwnersEntries_LinkedOrNot()
    {
        await using var context = CreateContext();
        var linked = Entry(1, _userId);
        var unlinked = Entry(null, _userId);
        var foreign = Entry(2, 99);
        context.BondEntries.AddRange(linked, unlinked, foreign);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        long[] ids = [linked.EntryId, unlinked.EntryId, foreign.EntryId];

        var entries = await new BondEntryRepository(context).GetByIds(_userId, ids, TestContext.Current.CancellationToken);

        Assert.Equal(ids[..2].Order(), entries.Select(e => e.EntryId).Order());
        Assert.Empty(await new BondEntryRepository(context).GetByIds(0, ids, TestContext.Current.CancellationToken));
    }
}