using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Dashboard.HttpClients;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Dashboard.Controllers;

/// <summary>
/// The dashboard overview is cached for five minutes per user and range. Owner transactions have no account, so this proves the
/// cache is still busted by the owner cash and trade routes and the overview shows the new pooled totals on the very next read.
/// </summary>
[Trait("Category", "Integration")]
public class DashboardOwnerTransactionInvalidationTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _owner = 1126;
    private const long _listing = 1126;
    private const int _pln = 0;
    private static readonly DateTime _day = DateTime.UtcNow.Date.AddDays(-2);
    private readonly Mock<IUserPlanVerifier> _planVerifier = new();
    private TestDatabase? _database;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        _database = new TestDatabase();
        services.AddSingleton(_database.Context);
        _planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(_planVerifier.Object);

        var prices = new Mock<IInvestmentPriceProvider>();
        prices.Setup(x => x.GetPricePerUnitAsync(_listing, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(10m);
        prices.Setup(x => x.GetPricePerUnitSeriesAsync(_listing, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, Currency _, DateTime start, DateTime end, CancellationToken _) =>
                Enumerable.Range(0, (end.Date - start.Date).Days + 1).ToDictionary(i => start.Date.AddDays(i), _ => 10m));
        services.RemoveAll<IInvestmentPriceProvider>();
        services.AddSingleton(prices.Object);
    }

    [Fact]
    public async Task OwnerCashEntryAndTrade_BustTheCachedOverview_AndShowUpInTheNextRead()
    {
        var ct = TestContext.Current.CancellationToken;
        var db = _database!.Context;
        db.Currencies.Add(new Currency(_pln, "PLN", "zł"));
        db.Assets.Add(new Asset
        {
            Id = 1126,
            Name = "Test fund",
            Type = AssetType.ETF,
            Listings = [new() { Id = _listing, Ticker = "OWNR", ExchangeMic = "XNAS", ExchangeName = "Nasdaq", TradingCurrency = "PLN" }]
        });
        await db.SaveChangesAsync(ct);
        Authorize("owner", _owner, UserRole.User);
        var dashboard = new DashboardHttpClient(Client);
        var start = DateTime.UtcNow.Date.AddDays(-7);
        var end = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

        var before = await dashboard.GetOverview(_owner, _pln, start, end);
        Assert.NotNull(before);
        Assert.Empty(before.AssetsPerType);
        Assert.Equal(0m, before.NetWorthSeries[^1].Value);

        using var cash = await Client.PostAsJsonAsync("api/transactions/cash", new CreateCashEntryRequest(_pln, _day, 500m, "unlinked cash", null), ct);
        Assert.Equal(HttpStatusCode.OK, cash.StatusCode);
        var afterCash = await dashboard.GetOverview(_owner, _pln, start, end);
        Assert.NotNull(afterCash);
        Assert.Equal(500m, afterCash.NetWorthSeries[^1].Value);
        var cashRow = Assert.Single(afterCash.AssetsPerAccount);
        Assert.Equal(("PLN", 500m, null), (cashRow.Name, cashRow.Value, cashRow.AccountId));
        Assert.Equal(("Cash", 500m), (Assert.Single(afterCash.AssetsPerType).Name, Assert.Single(afterCash.AssetsPerType).Value));

        using var trade = await Client.PostAsJsonAsync("api/transactions/investment",
            new CreateInvestmentTradeRequest(_listing, InvestmentTransactionTypeDto.Buy, 3m, 10m, "PLN", DateOnly.FromDateTime(_day)), ct);
        Assert.Equal(HttpStatusCode.Created, trade.StatusCode);
        var afterTrade = await dashboard.GetOverview(_owner, _pln, start, end);
        Assert.NotNull(afterTrade);
        Assert.Equal(530m, afterTrade.NetWorthSeries[^1].Value);
        Assert.Equal([("Cash", 500m), ("Stock", 30m)], afterTrade.AssetsPerType.Select(x => (x.Name, x.Value)));
        Assert.Equal([("PLN", 500m), ("OWNR", 30m)], afterTrade.AssetsPerAccount.Select(x => (x.Name, x.Value)));
        Assert.All(afterTrade.AssetsPerAccount, x => Assert.Null(x.AccountId));
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
        GC.SuppressFinalize(this);
    }
}