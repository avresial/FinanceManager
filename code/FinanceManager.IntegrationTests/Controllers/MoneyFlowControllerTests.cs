using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
public class MoneyFlowControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private DateTime _nowUtc;
    private int _valueChange = 10;
    private int _value;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _nowUtc = DateTime.UtcNow.Date;
        _testDatabase = new TestDatabase();

        // remove any registration for AppDbContext
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);

#pragma warning disable CS0854
        var currencyRepoMock = new Mock<ICurrencyRepository>();
        currencyRepoMock.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Range(0, 1).Select(_ => DefaultCurrency.USD));
#pragma warning restore CS0854

        services.AddSingleton(currencyRepoMock.Object);
    }

    private async Task SeedWithTestBankAccount(string accountName = "Test Bank Account")
    {
        if (await _testDatabase!.Context.Accounts.AnyAsync(x => x.Name == accountName))
            return;

        FinancialLabel salary = new() { Name = "salary" };
        _testDatabase!.Context.FinancialLabels.Add(salary);
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
        await _testDatabase.Context.SaveChangesAsync();

        for (DateTime i = _nowUtc.AddMonths(-2).Date; i <= _nowUtc; i = i.AddDays(1))
            _testDatabase!.Context.BankEntries.Add(new BankAccountEntry(test.AccountId, 0, i, _value += _valueChange, _valueChange)
            {
                Labels = [salary]
            });

        await _testDatabase.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetNetWorth_SingleDate_ReturnsValue()
    {
        await SeedWithTestBankAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc);

        Assert.NotNull(result);
        Assert.Equal(_value, result);
    }

    [Fact]
    public async Task GetNetWorth_Range_ReturnsDictionary()
    {
        await SeedWithTestBankAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc.AddDays(-7), _nowUtc);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetIncome_ReturnsList()
    {
        await SeedWithTestBankAccount();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetIncome(1, DefaultCurrency.USD, _nowUtc.AddDays(-days), _nowUtc);

        Assert.NotNull(result);
        Assert.All(result, x => Assert.Equal(_valueChange, x.Value));
    }

    [Fact]
    public async Task GetSpending_ReturnsList()
    {
        await SeedWithTestBankAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetSpending(1, DefaultCurrency.USD, _nowUtc.AddDays(-7), _nowUtc);

        Assert.NotNull(result);
        Assert.Equal(0, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetLabelsValue_ReturnsList()
    {
        await SeedWithTestBankAccount();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetLabelsValue(1, _nowUtc.AddDays(-days), _nowUtc);

        Assert.NotNull(result);
        Assert.Equal((1 + days) * _valueChange, result.First().Value);
    }

    [Fact]
    public async Task GetInvestmentRate_ReturnsList()
    {
        await SeedWithTestBankAccount();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetInvestmentRate(1, _nowUtc.AddDays(-days), _nowUtc).ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal((1 + days) * _valueChange, result.First().Salary);
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