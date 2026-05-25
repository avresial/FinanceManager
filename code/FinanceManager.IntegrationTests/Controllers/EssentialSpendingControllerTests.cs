using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class EssentialSpendingControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private readonly DateTime _nowUtc = DateTime.UtcNow.Date;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase.Context);

#pragma warning disable CS0854
        var currencyRepoMock = new Mock<ICurrencyRepository>();
        currencyRepoMock.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Range(0, 1).Select(_ => DefaultCurrency.USD));
#pragma warning restore CS0854

        services.AddSingleton(currencyRepoMock.Object);
    }

    private async Task SeedEssentialSpendingAccount()
    {
        var essentialLabel = new FinancialLabel
        {
            Name = "Rent",
            Classifications =
            [
                new FinancialLabelClassification
                {
                    Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind,
                    Value = FinancialLabelClassificationCatalog.EssentialValue
                }
            ]
        };

        var wantLabel = new FinancialLabel
        {
            Name = "Entertainment",
            Classifications =
            [
                new FinancialLabelClassification
                {
                    Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind,
                    Value = FinancialLabelClassificationCatalog.WantValue
                }
            ]
        };

        _testDatabase!.Context.FinancialLabels.AddRange(essentialLabel, wantLabel);

        var account = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 1,
            Name = "Household",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };

        _testDatabase.Context.Accounts.Add(account);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(1, 1, _nowUtc.AddDays(-1), 950m, -50m)
            {
                Labels = [essentialLabel]
            },
            new CurrencyAccountEntry(1, 2, _nowUtc, 930m, -20m)
            {
                Labels = [essentialLabel, wantLabel, essentialLabel]
            },
            new CurrencyAccountEntry(1, 3, _nowUtc, 920m, -10m)
            {
                Labels = [essentialLabel, wantLabel]
            });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetEssentialSpending_ReturnsOnlyResolvedEssentialOutflows()
    {
        await SeedEssentialSpendingAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new EssentialSpendingHttpClient(Client).GetEssentialSpending(1, DefaultCurrency.USD, _nowUtc.AddDays(-1), _nowUtc);

        Assert.Equal(2, result.Count);
        Assert.Equal(-50m, result.Single(x => x.DateTime == _nowUtc.AddDays(-1)).Value);
        Assert.Equal(-20m, result.Single(x => x.DateTime == _nowUtc).Value);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}