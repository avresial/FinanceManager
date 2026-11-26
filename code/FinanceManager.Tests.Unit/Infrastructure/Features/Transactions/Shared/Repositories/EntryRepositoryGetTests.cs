using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Transactions.Shared.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public sealed class EntryRepositoryGetTests
{
    [Fact]
    public async Task CurrencyGet_IncludesLabels()
    {
        await using var context = CreateContext();
        var label = new FinancialLabel { Name = "Groceries" };
        var entry = new CashEntry(null, 10, DateTime.UtcNow, 100, -25) { UserId = 1, Labels = [label] };
        context.CurrencyEntries.Add(entry);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var result = await new CashEntryRepository(context).GetByOwner(1, 10, TestContext.Current.CancellationToken);

        Assert.Equal("Groceries", Assert.Single(Assert.IsType<CashEntry>(result).Labels).Name);
    }

    [Fact]
    public async Task BondGet_IncludesLabels()
    {
        await using var context = CreateContext();
        var label = new FinancialLabel { Name = "Interest" };
        var entry = new BondEntry(null, 20, DateTime.UtcNow, 100, 5, 7) { UserId = 2, Labels = [label] };
        context.BondEntries.Add(entry);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var result = await new BondEntryRepository(context).GetByOwner(2, 20, TestContext.Current.CancellationToken);

        Assert.Equal("Interest", Assert.Single(Assert.IsType<BondEntry>(result).Labels).Name);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}