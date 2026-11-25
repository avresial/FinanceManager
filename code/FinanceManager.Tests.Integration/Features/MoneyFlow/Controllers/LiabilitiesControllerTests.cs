using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.MoneyFlow.Controllers;

[Trait("Category", "Integration")]
public class LiabilitiesControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase _testDatabase = null!;
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

        services.AddSingleton(_testDatabase.Context);
    }

    private async Task SeedCashEntries()
    {
        _value = _valueChange;

        int days = 100;
        _value = (2 + days) * _valueChange * -1;
        // Balances are the sum of the entries' changes, so the debt is booked as an opening entry the later repayments work off.
        _testDatabase.Context.CurrencyEntries.Add(new CashEntry(null, 0, DateTime.UtcNow.AddDays(-days - 1).Date, _value, _value) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id });

        for (DateTime i = DateTime.UtcNow.AddDays(-days).Date; i <= DateTime.UtcNow; i = i.AddDays(1))
            _testDatabase.Context.CurrencyEntries.Add(new CashEntry(null, 0, i, _value += _valueChange, _valueChange) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task IsAnyAccountWithLiabilities_ReturnsTrue()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new LiabilitiesHttpClient(Client).IsAnyAccountWithLiabilities(1);

        Assert.True(result);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerAccount_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new LiabilitiesHttpClient(Client).GetEndLiabilitiesPerAccount(1, _nowUtc.AddDays(-1), _nowUtc)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("PLN", result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerType_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new LiabilitiesHttpClient(Client).GetEndLiabilitiesPerType(1, _nowUtc.AddDays(-1), _nowUtc)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Cash", result[0].Name);
        Assert.Equal(_value, result[0].Value);
    }

    [Fact]
    public async Task GetLiabilitiesTimeSeries_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new LiabilitiesHttpClient(Client).GetLiabilitiesTimeSeries(1, _nowUtc.AddDays(-100), _nowUtc)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, item => Assert.True(item.Value < 0));
    }

    [Fact]
    public async Task GetEndLiabilitiesPerAccount_ForOtherUser_ReturnsForbidden()
    {
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.GetAsync($"api/Liabilities/GetEndLiabilitiesPerAccount/2/{_nowUtc.AddDays(-2):O}/{_nowUtc:O}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null!;
        GC.SuppressFinalize(this);
    }
}