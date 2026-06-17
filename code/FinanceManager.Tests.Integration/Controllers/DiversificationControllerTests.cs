using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class DiversificationControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private DateTime _nowUtc;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _nowUtc = DateTime.UtcNow.Date;
        _testDatabase = new TestDatabase();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);
    }

    private async Task SeedPortfolio()
    {
        var stockAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 1,
            Name = "Stocks",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Stock
        };
        var bondAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 2,
            Name = "Bonds",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Bond
        };
        var cashAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 3,
            Name = "Cash",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };
        _testDatabase!.Context.Accounts.AddRange(stockAccount, bondAccount, cashAccount);

        var usd = new Currency(1, "USD", "$");
        _testDatabase.Context.Currencies.Add(usd);
        _testDatabase.Context.StockDetails.Add(new StockDetails
        {
            Isin = "US0378331005",
            Ticker = "AAPL",
            Name = "Apple Inc.",
            Type = "Stock",
            Region = "US",
            Currency = usd
        });

        var bond = new BondDetails("Treasury 2030", "Test Issuer",
            DateOnly.FromDateTime(_nowUtc.AddYears(-1)), DateOnly.FromDateTime(_nowUtc.AddYears(5)), [])
        { Id = 5 };
        _testDatabase.Context.Bonds.Add(bond);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.StockEntries.Add(
            new StockAccountEntry(1, 1, _nowUtc.AddDays(-3), 100m, 100m, "US0378331005", InvestmentType.Stock));
        _testDatabase.Context.BondEntries.Add(
            new BondAccountEntry(2, 1, _nowUtc.AddDays(-3), 1000m, 1000m, 5));
        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(3, 1, _nowUtc.AddDays(-3), 500m, 500m) { Labels = [] });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_ReturnsHoldingsGroupedByClass()
    {
        await SeedPortfolio();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new DiversificationHttpClient(Client).GetDiversificationBreakdown(1, _nowUtc);

        Assert.NotNull(result);
        Assert.Equal(["Stocks", "Bonds", "Cash"], result.AssetClasses.Select(g => g.AssetClass));
        Assert.Equal(["AAPL"], result.AssetClasses[0].Holdings);
        Assert.Equal(["Treasury 2030"], result.AssetClasses[1].Holdings);
        Assert.Equal(["Cash"], result.AssetClasses[2].Holdings);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_EmptyPortfolio_ReturnsNoGroups()
    {
        Authorize("EmptyUser", 99, UserRole.User);

        var result = await new DiversificationHttpClient(Client).GetDiversificationBreakdown(99, _nowUtc);

        Assert.NotNull(result);
        Assert.Empty(result.AssetClasses);
    }

    [Fact]
    public async Task GetDiversificationBreakdown_ForOtherUser_ReturnsForbidden()
    {
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.GetAsync($"api/Diversification/2/{_nowUtc:O}/breakdown", TestContext.Current.CancellationToken);

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