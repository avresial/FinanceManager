using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Bond.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class BondDetailsRepositoryTests
{
    [Fact]
    public async Task GetByIdsAsync_ReturnsRequestedDetachedDefinitionsWithRelatedData()
    {
        await using var context = CreateContext();
        var currency = new Currency(1, "PLN", "zł");
        var first = CreateBond("First", currency);
        var second = CreateBond("Second", currency);
        context.Currencies.Add(currency);
        context.Bonds.AddRange(first, second);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var repository = new BondDetailsRepository(context);
        var result = await repository.GetByIdsAsync([first.Id, 999], TestContext.Current.CancellationToken);

        var returned = Assert.Single(result);
        Assert.Equal(first.Id, returned.Id);
        Assert.Equal("First", returned.Name);
        Assert.Equal(currency.Id, returned.Currency.Id);
        Assert.Single(returned.CalculationMethods);
        Assert.Same(returned, returned.CalculationMethods[0].BondDetails);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdsAsync_WithNoIds_ReturnsEmptyWithoutMaterializingDefinitions()
    {
        await using var context = CreateContext();
        var repository = new BondDetailsRepository(context);

        var result = await repository.GetByIdsAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BondDetails CreateBond(string name, Currency currency) =>
        new(
            name,
            "Issuer",
            new DateOnly(2024, 1, 1),
            new DateOnly(2025, 1, 1),
            [new BondCalculationMethod
            {
                DateOperator = DateOperator.UntilDate,
                DateValue = "2025-01-01",
                Rate = 0.01m,
            }],
            currency,
            BondType.InflationBond,
            100m);
}