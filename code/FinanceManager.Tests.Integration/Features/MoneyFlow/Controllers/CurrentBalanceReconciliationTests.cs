using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared.Services;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.MoneyFlow.Controllers;

[Trait("Category", "Integration")]
public class CurrentBalanceReconciliationTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _userId = 951;
    private const int _currencyId = 985;
    private static readonly DateTime _asOf = new(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private TestDatabase? _database;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        _database = new TestDatabase();
        services.AddSingleton(_database.Context);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(_asOf);
        services.RemoveAll<IDateTimeProvider>();
        services.AddSingleton(clock.Object);
        var plan = new Mock<IUserPlanVerifier>();
        plan.Setup(x => x.CanAddMoreEntries(_userId, It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(plan.Object);
    }

    [Fact]
    public async Task CurrentBalances_ReconcileEquivalentScopesAndRefreshAfterBackdatedWrite()
    {
        var context = _database!.Context;
        context.Currencies.Add(new Currency(_currencyId, "PLN", "PLN"));
        context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(null, 1, _asOf.Date.AddDays(-2), 900m, 900m) { UserId = _userId, CurrencyId = _currencyId },
            new CurrencyAccountEntry(null, 2, _asOf.AddMinutes(-1), 1000m, 100m) { UserId = _userId, CurrencyId = _currencyId },
            new CurrencyAccountEntry(null, 3, _asOf.AddMinutes(-1), 634m, -366m) { UserId = _userId, CurrencyId = _currencyId },
            new CurrencyAccountEntry(null, 4, _asOf.AddMinutes(1), 9634m, 9000m) { UserId = _userId, CurrencyId = _currencyId },
            new CurrencyAccountEntry(null, 5, _asOf.Date.AddDays(-2), -200m, -200m) { UserId = _userId, CurrencyId = _currencyId });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        // From here on every write goes through the real repositories, which must stamp the owner keys themselves.
        Authorize("current-balances", _userId, UserRole.User);

        await AssertCurrentBalances(634m);
        // A midnight historical end remains an exact cutoff, rather than expanding to end-of-day.
        var historical = await Client.GetFromJsonAsync<List<TimeSeriesModel>>(
            $"api/MoneyFlow/GetClosingBalance/{_userId}/{_currencyId}/{_asOf.Date.AddDays(-2):O}/{_asOf.Date:O}",
            TestContext.Current.CancellationToken);
        Assert.Equal(700m, historical![^1].Value);

        var write = await Client.PostAsJsonAsync("api/transactions/cash", new
        {
            CurrencyId = _currencyId,
            PostingDate = _asOf.Date.AddDays(-1),
            ValueChange = -10m,
            Description = "Backdated expense"
        }, TestContext.Current.CancellationToken);
        write.EnsureSuccessStatusCode();

        // The identical overview request must refresh after the normal write invalidates its cache.
        await AssertCurrentBalances(624m);
    }

    private async Task AssertCurrentBalances(decimal expectedCash)
    {
        var start = _asOf.Date.AddDays(-2);
        var overview = await Client.GetFromJsonAsync<DashboardOverviewDto>(
            $"api/Dashboard/overview?userId={_userId}&currencyId={_currencyId}&start={start:O}&end={_asOf:O}",
            TestContext.Current.CancellationToken);
        var cashSeries = await Client.GetFromJsonAsync<List<TimeSeriesModel>>(
            $"api/MoneyFlow/GetClosingBalance/{_userId}/{_currencyId}/{start:O}/{_asOf:O}",
            TestContext.Current.CancellationToken);
        var forecast = await Client.GetFromJsonAsync<CashFlowForecast>(
            $"api/CashFlowForecast?userId={_userId}&currencyId={_currencyId}&horizonDays=30",
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCash - 200m, cashSeries![^1].Value);
        // The forecast pools every owned cash entry, including the one on the loan-typed account, like net worth does.
        Assert.Equal(expectedCash - 200m, forecast!.HistoricalSeries[^1].Value);
        Assert.Equal(expectedCash - 200m, forecast.ForecastSeries[0].Value);
        Assert.Equal(expectedCash - 200m, overview!.NetWorthSeries[^1].Value);
        Assert.Equal(overview.NetWorthSeries[^1].Value, overview.ClosingBalanceSeries[^1].Value);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
    }
}