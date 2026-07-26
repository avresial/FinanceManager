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

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}