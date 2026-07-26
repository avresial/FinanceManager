using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Currencies.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class CurrencyControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);
    }

    [Fact]
    public async Task GetAll_Anonymous_IsUnauthorized()
    {
        ClearAuthorization();

        var response = await Client.GetAsync("api/Currency/GetAll", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsPredefinedCurrenciesSortedByCode()
    {
        Authorize("TestUser", 1, UserRole.User);

        var currencies = await Client.GetFromJsonAsync<List<Currency>>("api/Currency/GetAll", TestContext.Current.CancellationToken);

        Assert.NotNull(currencies);
        Assert.Contains(currencies, x => x.ShortName == DefaultCurrency.PLN.ShortName);
        Assert.Contains(currencies, x => x.ShortName == DefaultCurrency.USD.ShortName);
        foreach (var (shortName, _) in DefaultCurrency.CommonCurrencies)
            Assert.Contains(currencies, x => x.ShortName == shortName);

        Assert.Equal(currencies.OrderBy(x => x.ShortName, StringComparer.Ordinal).Select(x => x.ShortName), currencies.Select(x => x.ShortName));
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        GC.SuppressFinalize(this);
    }
}