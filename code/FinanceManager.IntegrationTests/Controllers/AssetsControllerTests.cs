using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class AssetsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private DateTime _nowUtc;
    private int _valueChange = 10;
    private int _value;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Legacy individual seeder hosted services removed; no need to unregister.

        _nowUtc = DateTime.UtcNow;
        _testDatabase = new TestDatabase();

        // remove any registration for AppDbContext
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);

    }

    private async Task SeedWithTestCurrencyAccount(string accountName = "Test Currency Account")
    {

        if (await _testDatabase!.Context.Accounts.AnyAsync(x => x.Name == accountName, TestContext.Current.CancellationToken))
            return;

        _value = _valueChange;

        var test = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 1,
            Name = accountName,
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };

        _testDatabase!.Context.Accounts.Add(test);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        for (DateTime i = DateTime.UtcNow.AddMonths(-24).Date; i <= DateTime.UtcNow; i = i.AddDays(1))
            _testDatabase!.Context.CurrencyEntries.Add(new CurrencyAccountEntry(test.AccountId, 0, i, _value += _valueChange, _valueChange));

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task IsAnyAccountWithAssets_ReturnsTrue()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).IsAnyAccountWithAssets(1);

        Assert.True(result);
    }

    [Fact]
    public async Task GetEndAssetsPerAccount_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetEndAssetsPerAccount(1, DefaultCurrency.USD, _nowUtc);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Currency Account", result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetEndAssetsPerType_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetEndAssetsPerType(1, DefaultCurrency.USD, _nowUtc);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(AccountLabel.Cash.ToString(), result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetAssetsTimeSeries(1, DefaultCurrency.USD, _nowUtc.AddDays(-2), _nowUtc);

        Assert.NotNull(result);
        Assert.NotEmpty(result);

        Assert.All(result, item => Assert.True(item.Value > 0));
    }

    [Fact]
    public async Task GetInvestmentPaycheckEstimate_ReturnsPartialSalaryHistoryMetadata()
    {
        var salary = new FinancialLabel { Name = "salary" };
        _testDatabase!.Context.FinancialLabels.Add(salary);

        var salaryAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 10,
            Name = "Salary Account",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };

        var bondAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 11,
            Name = "Bond Portfolio",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Bond
        };

        _testDatabase.Context.Accounts.AddRange(salaryAccount, bondAccount);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(salaryAccount.AccountId, 1, _nowUtc.AddMonths(-2), 3000m, 3000m) { Labels = [salary] },
            new CurrencyAccountEntry(salaryAccount.AccountId, 2, _nowUtc, 4500m, 4500m) { Labels = [salary] });
        _testDatabase.Context.BondEntries.Add(new BondAccountEntry(bondAccount.AccountId, 1, _nowUtc.AddDays(-1), 12000m, 12000m, 1));

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new AssetsHttpClient(Client).GetInvestmentPaycheckEstimate(1, DefaultCurrency.USD, _nowUtc, 0.05m, 3);

        Assert.NotNull(result);
        Assert.Equal(12000m, result.InvestableAssetsValue);
        Assert.Equal(50m, result.SustainableMonthlyPaycheck);
        Assert.Equal(3, result.SalaryMonthsRequested);
        Assert.Equal(2, result.SalaryMonthsUsed);
        Assert.Equal(3750m, result.AverageMonthlySalary);
        Assert.True(result.HasPartialSalaryHistory);
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