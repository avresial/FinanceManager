using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Dashboard.Controllers;

// The dashboard's default range ends at the end of today (UTC). The real overview aggregation must
// count entries posted later today — not clamp them away at "now" — while still leaving out later days. #1120
[Trait("Category", "Integration")]
public class DashboardDefaultRangeCutoffTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _userId = 1120;
    private const int _cashAccountId = 11201;
    private const int _loanAccountId = 11202;
    private const int _currencyId = 1120;
    private TestDatabase? _database;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        _database = new TestDatabase();
        services.AddSingleton(_database.Context);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DefaultRange_IncludesLaterToday_AndKeepsExactRequestsIsolated(bool defaultFirst)
    {
        var (start, end) = DateRangeHelper.GetDefaultDashboardRange(DateTime.UtcNow);
        var today = end.Date;
        var laterToday = end;
        var tomorrow = today.AddDays(1).AddHours(12);

        var context = _database!.Context;
        context.Currencies.Add(new Currency(_currencyId, "PLN", "PLN"));
        context.Accounts.AddRange(
            new FinancialAccountBaseDto { AccountId = _cashAccountId, UserId = _userId, Name = "Cash", AccountType = AccountType.Currency, AccountLabel = AccountLabel.Cash },
            new FinancialAccountBaseDto { AccountId = _loanAccountId, UserId = _userId, Name = "Loan", AccountType = AccountType.Currency, AccountLabel = AccountLabel.Loan });
        context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(_cashAccountId, 1, today.AddDays(-2), 1000m, 1000m),
            new CurrencyAccountEntry(_cashAccountId, 2, laterToday, 1250m, 250m),
            new CurrencyAccountEntry(_cashAccountId, 3, tomorrow, 9250m, 8000m),
            new CurrencyAccountEntry(_loanAccountId, 4, today.AddDays(-2), -300m, -300m),
            new CurrencyAccountEntry(_loanAccountId, 5, laterToday, -350m, -50m));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("dashboard-cutoff", _userId, UserRole.User);

        var url = $"api/Dashboard/overview?userId={_userId}&currencyId={_currencyId}&start={Uri.EscapeDataString(start.ToString("O"))}&end={Uri.EscapeDataString(end.ToString("O"))}";
        async Task<DashboardOverviewDto> Read(bool defaultRange) =>
            Assert.IsType<DashboardOverviewDto>(await Client.GetFromJsonAsync<DashboardOverviewDto>(
                defaultRange ? $"{url}&defaultRange=true" : url, TestContext.Current.CancellationToken));

        var first = await Read(defaultFirst);
        var second = await Read(!defaultFirst);
        var overview = defaultFirst ? first : second;
        var exact = defaultFirst ? second : first;
        Assert.Equal(700m, exact.NetWorthSeries[^1].Value);
        Assert.Equal(900m, (await Read(true)).NetWorthSeries[^1].Value);
        // The response is labelled with the exact range requested, so cards pair dates and amounts.
        Assert.Equal(start, overview.StartDate.ToUniversalTime());
        Assert.Equal(end, overview.EndDate.ToUniversalTime());

        // Today's point holds the later-today entries (1250 cash, -350 loan); tomorrow's 8000 is excluded.
        Assert.Equal(today, overview.NetWorthSeries[^1].DateTime.Date);
        Assert.Equal(900m, overview.NetWorthSeries[^1].Value);
        Assert.Equal(900m, overview.ClosingBalanceSeries[^1].Value);
        Assert.Equal(900m, overview.NetCashFlowSeries.Sum(point => point.Value));
        Assert.Equal(-350m, Assert.Single(overview.LiabilitiesPerAccount).Value);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
    }
}