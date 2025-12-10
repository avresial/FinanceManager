using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
public class InflationControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
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
    public async Task GetInflationRate_WithValidData_ReturnsInflationRate()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.User);
        var client = new InflationHttpClient(Client);
        var currencyId = 1; // PLN
        var date = new DateOnly(2023, 1, 1);

        // Act
        var result = await client.GetInflationRateAsync(currencyId, date, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(currencyId, result.CurrencyId);
        Assert.Equal(date, result.Date);
        Assert.True(result.Rate > 0);
    }

    [Fact]
    public async Task GetInflationRate_WithInvalidDate_ReturnsNull()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.User);
        var client = new InflationHttpClient(Client);
        var currencyId = 1;
        var date = new DateOnly(2030, 12, 31); // Future date with no data

        // Act
        var result = await client.GetInflationRateAsync(currencyId, date, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetInflationRates_WithValidRange_ReturnsRates()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.User);
        var client = new InflationHttpClient(Client);
        var currencyId = 1; // PLN
        var from = new DateOnly(2023, 1, 1);
        var to = new DateOnly(2023, 3, 1);

        // Act
        var result = await client.GetInflationRatesAsync(currencyId, from, to, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var rates = result.ToList();
        Assert.Equal(3, rates.Count);
        Assert.All(rates, r => Assert.True(r.Rate > 0));
    }

    [Fact]
    public async Task GetInflationRates_WithEmptyRange_ReturnsEmptyList()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.User);
        var client = new InflationHttpClient(Client);
        var currencyId = 1;
        var from = new DateOnly(2030, 1, 1); // Future dates with no data
        var to = new DateOnly(2030, 12, 31);

        // Act
        var result = await client.GetInflationRatesAsync(currencyId, from, to, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetInflationRate_WithoutAuth_ThrowsUnauthorized()
    {
        // Arrange - No authorization
        var client = new InflationHttpClient(Client);
        var currencyId = 1;
        var date = new DateOnly(2023, 1, 1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.GetInflationRateAsync(currencyId, date, TestContext.Current.CancellationToken)
        );
        Assert.Contains("401", exception.Message);
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