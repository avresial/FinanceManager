using FinanceManager.Components.HttpClients;
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
public class LiabilitiesControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private DateTime _nowUtc;
    private int _valueChange = 10;
    private int _value;

    protected override void ConfigureServices(IServiceCollection services)
    {
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
            AccountLabel = AccountLabel.Loan,
            AccountType = AccountType.Currency
        };

        _testDatabase!.Context.Accounts.Add(test);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        int days = 100;
        _value = (2 + days) * _valueChange * -1;

        for (DateTime i = DateTime.UtcNow.AddDays(-days).Date; i <= DateTime.UtcNow; i = i.AddDays(1))
            _testDatabase!.Context.CurrencyEntries.Add(new CurrencyAccountEntry(test.AccountId, 0, i, _value += _valueChange, _valueChange));

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task IsAnyAccountWithLiabilities_ReturnsTrue()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new LiabilitiesHttpClient(Client).IsAnyAccountWithLiabilities(1);

        Assert.True(result);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerAccount_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new LiabilitiesHttpClient(Client).GetEndLiabilitiesPerAccount(1, _nowUtc.AddDays(-1), _nowUtc)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Currency Account", result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerType_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new LiabilitiesHttpClient(Client).GetEndLiabilitiesPerType(1, _nowUtc.AddDays(-1), _nowUtc)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(AccountLabel.Loan.ToString(), result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetLiabilitiesTimeSeries_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new LiabilitiesHttpClient(Client).GetLiabilitiesTimeSeries(1, _nowUtc.AddDays(-100), _nowUtc)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, item => Assert.True(item.Value < 0));
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