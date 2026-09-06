using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public sealed class EntryRepositoryGetTests
{
    [Fact]
    public async Task CurrencyGet_IncludesLabels()
    {
        await using var context = CreateContext();
        var label = new FinancialLabel { Name = "Groceries" };
        var entry = new CurrencyAccountEntry(1, 10, DateTime.UtcNow, 100, -25) { Labels = [label] };
        context.CurrencyEntries.Add(entry);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var result = await new CurrencyEntryRepository(context).Get(1, 10);

        Assert.Equal("Groceries", Assert.Single(Assert.IsType<CurrencyAccountEntry>(result).Labels).Name);
    }

    [Fact]
    public async Task BondGet_IncludesLabels()
    {
        await using var context = CreateContext();
        var label = new FinancialLabel { Name = "Interest" };
        var entry = new BondAccountEntry(2, 20, DateTime.UtcNow, 100, 5, 7) { Labels = [label] };
        context.BondEntries.Add(entry);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var result = await new BondEntryRepository(context).Get(2, 20);

        Assert.Equal("Interest", Assert.Single(Assert.IsType<BondAccountEntry>(result).Labels).Name);
    }

    [Fact]
    public async Task CurrencyGetMostRecentByAccounts_IsBoundedAndOrderedAcrossAccounts()
    {
        await using var context = CreateContext();
        context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(1, 10, new DateTime(2024, 3, 1), 100, 1),
            new CurrencyAccountEntry(2, 11, new DateTime(2024, 3, 3), 100, 1),
            new CurrencyAccountEntry(1, 12, new DateTime(2024, 3, 2), 100, 1),
            new CurrencyAccountEntry(3, 13, new DateTime(2024, 3, 4), 100, 1));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new CurrencyEntryRepository(context)
            .GetMostRecentByAccounts([1, 2], 2, TestContext.Current.CancellationToken);

        Assert.Equal([11, 12], result.Select(entry => entry.EntryId));
    }

    [Fact]
    public async Task BondGetMostRecentByAccounts_IsBoundedAndOrderedAcrossAccounts()
    {
        await using var context = CreateContext();
        context.BondEntries.AddRange(
            new BondAccountEntry(1, 10, new DateTime(2024, 3, 1), 100, 1, 7),
            new BondAccountEntry(2, 11, new DateTime(2024, 3, 3), 100, 1, 7),
            new BondAccountEntry(1, 12, new DateTime(2024, 3, 2), 100, 1, 7),
            new BondAccountEntry(3, 13, new DateTime(2024, 3, 4), 100, 1, 7));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new BondEntryRepository(context)
            .GetMostRecentByAccounts([1, 2], 2, TestContext.Current.CancellationToken);

        Assert.Equal([11, 12], result.Select(entry => entry.EntryId));
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}