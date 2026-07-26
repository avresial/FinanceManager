using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class CurrencyRepositoryTests
{
    [Fact]
    public async Task GetCurrency_WhenPresent_DoesNotRunDefaultSeeding()
    {
        await using var context = CreateContext();
        context.Currencies.Add(new Currency(99, "EUR", "€"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var currency = await new CurrencyRepository(context).GetCurrency(99, TestContext.Current.CancellationToken);

        Assert.Equal("EUR", currency!.ShortName);
        Assert.Equal(1, await context.Currencies.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetByCode_WhenMissing_StillSeedsDefaults()
    {
        await using var context = CreateContext();

        var currency = await new CurrencyRepository(context).GetByCode("PLN", TestContext.Current.CancellationToken);

        Assert.Equal(DefaultCurrency.PLN.ShortName, currency!.ShortName);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}