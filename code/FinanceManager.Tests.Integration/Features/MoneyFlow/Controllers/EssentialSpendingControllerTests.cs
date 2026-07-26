using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.MoneyFlow.Controllers;

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

        // A second, distinct Essential label so an entry can have a 2-to-1 Essential majority via
        // separate labels. The many-to-many join table keys on (EntryId, LabelId), so attaching the
        // same label twice never persists a second row — a real Essential majority needs distinct labels.
        var essentialLabel2 = new FinancialLabel
        {
            Name = "Groceries",
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

        _testDatabase!.Context.FinancialLabels.AddRange(essentialLabel, essentialLabel2, wantLabel);

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
                Labels = [essentialLabel, wantLabel, essentialLabel2]
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

    [Fact]
    public async Task GetEssentialSpending_ForOtherUser_ReturnsForbidden()
    {
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.GetAsync($"api/EssentialSpending/GetEssentialSpending/2/{DefaultCurrency.USD.Id}/{_nowUtc.AddDays(-1):O}/{_nowUtc:O}/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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