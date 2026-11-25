using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Dashboard.Controllers;

// The dashboard requests whole UTC days (yyyy-MM-dd), so its range ends at the last tick of today. The real
// overview aggregation must count entries posted later today — not clamp them away at "now" — while still
// leaving out later days, and exact intraday timestamps must keep their own cutoff. #1120
[Trait("Category", "Integration")]
public class DashboardDefaultRangeCutoffTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _userId = 1120;
    private const int _currencyId = 1120;
    private const int _loanCurrencyId = 1121;
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
    public async Task DateOnlyRange_IncludesLaterToday_AndKeepsExactRequestsIsolated(bool dateOnlyFirst)
    {
        var (start, end) = DateRangeHelper.GetDefaultDashboardRange(DateTime.UtcNow);
        var today = end.Date;
        var laterToday = end;
        var tomorrow = today.AddDays(1).AddHours(12);

        var context = _database!.Context;
        // Cash pools per currency, so the loan sits in its own currency to stay a liability next to the cash.
        context.Currencies.AddRange(new Currency(_currencyId, "PLN", "PLN"), new Currency(_loanCurrencyId, "EUR", "EUR"));
        var label = new FinancialLabel
        {
            Name = "Loan payment",
            Classifications = [new FinancialLabelClassification
            {
                Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind,
                Value = FinancialLabelClassificationCatalog.EssentialValue
            }]
        };
        context.FinancialLabels.Add(label);
        context.CurrencyEntries.AddRange(
            new CashEntry(1, today.AddDays(-2), 1000m) { UserId = _userId, CurrencyId = _currencyId },
            new CashEntry(2, laterToday, 250m) { UserId = _userId, CurrencyId = _currencyId },
            new CashEntry(3, tomorrow, 8000m) { UserId = _userId, CurrencyId = _currencyId },
            new CashEntry(4, today.AddDays(-2), -300m) { UserId = _userId, CurrencyId = _loanCurrencyId },
            new CashEntry(5, laterToday, -50m) { UserId = _userId, CurrencyId = _loanCurrencyId, Labels = [label] },
            new CashEntry(6, tomorrow, -75m) { UserId = _userId, CurrencyId = _loanCurrencyId, Labels = [label] });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("dashboard-cutoff", _userId, UserRole.User);

        // The dashboard sends whole UTC days; exact intraday callers still send ISO timestamps.
        var dateOnly = (Start: start.ToString("yyyy-MM-dd"), End: today.ToString("yyyy-MM-dd"));
        var intraday = (Start: start.ToString("O"), End: DateTime.UtcNow.ToString("O"));
        async Task<DashboardOverviewDto> Read((string Start, string End) range) =>
            Assert.IsType<DashboardOverviewDto>(await Client.GetFromJsonAsync<DashboardOverviewDto>(
                $"api/Dashboard/overview?userId={_userId}&currencyId={_currencyId}&start={Uri.EscapeDataString(range.Start)}&end={Uri.EscapeDataString(range.End)}",
                TestContext.Current.CancellationToken));

        var first = await Read(dateOnlyFirst ? dateOnly : intraday);
        var second = await Read(dateOnlyFirst ? intraday : dateOnly);
        var overview = dateOnlyFirst ? first : second;
        var exact = dateOnlyFirst ? second : first;
        Assert.Equal(700m, exact.NetWorthSeries[^1].Value);
        Assert.Equal(900m, (await Read(dateOnly)).NetWorthSeries[^1].Value);

        // An ISO timestamp at the day's last tick (in any offset) means the same range as the bare date.
        var endOfDayInOffset = new DateTimeOffset(end).ToOffset(TimeSpan.FromHours(2)).ToString("O");
        var endOfDay = await Read((start.ToString("O"), endOfDayInOffset));
        Assert.Equal(900m, endOfDay.NetWorthSeries[^1].Value);
        Assert.Equal(-50m, Assert.Single(endOfDay.LabelsValue, row => row.Name == label.Name).Value);

        // The date-only response is labelled with the UTC midnight start and the inclusive last-tick end.
        Assert.Equal(start, overview.StartDate.ToUniversalTime());
        Assert.Equal(end, overview.EndDate.ToUniversalTime());

        // Today's point holds the later-today entries (1250 cash, -350 loan); tomorrow's 8000 is excluded.
        Assert.Equal(today, overview.NetWorthSeries[^1].DateTime.Date);
        Assert.Equal(900m, overview.NetWorthSeries[^1].Value);
        Assert.Equal(900m, overview.ClosingBalanceSeries[^1].Value);
        Assert.Equal(900m, overview.NetCashFlowSeries.Sum(point => point.Value));
        var liability = Assert.Single(overview.LiabilitiesPerAccount);
        Assert.Equal(("EUR", -350m), (liability.Name, liability.Value));
        Assert.Equal(-50m, Assert.Single(overview.LabelsValue, row => row.Name == label.Name).Value);
        Assert.Equal(0m, Assert.Single(exact.LabelsValue, row => row.Name == label.Name).Value);
        var essential = FinancialLabelClassificationCatalog.EssentialValue;
        Assert.Equal(50m, Assert.Single(overview.ExpenseDistribution, row => row.Name == essential).Value);
        Assert.DoesNotContain(exact.ExpenseDistribution, row => row.Name == essential);
    }

    [Fact]
    public async Task LoanInTheSameCurrencyAsCash_NetsAgainstItInsteadOfShowingAsALiability()
    {
        var today = DateTime.UtcNow.Date;
        var context = _database!.Context;
        context.Currencies.Add(new Currency(_currencyId, "PLN", "PLN"));
        context.CurrencyEntries.AddRange(
            new CashEntry(1, today.AddDays(-2), 1000m) { UserId = _userId, CurrencyId = _currencyId },
            new CashEntry(2, today.AddDays(-2), -300m) { UserId = _userId, CurrencyId = _currencyId });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("dashboard-cutoff", _userId, UserRole.User);
        var (start, end) = DateRangeHelper.GetDefaultDashboardRange(DateTime.UtcNow);

        var overview = Assert.IsType<DashboardOverviewDto>(await Client.GetFromJsonAsync<DashboardOverviewDto>(
            $"api/Dashboard/overview?userId={_userId}&currencyId={_currencyId}&start={start:yyyy-MM-dd}&end={end.Date:yyyy-MM-dd}",
            TestContext.Current.CancellationToken));

        Assert.Empty(overview.LiabilitiesPerType);
        Assert.Empty(overview.LiabilitiesPerAccount);
        Assert.Equal(700m, Assert.Single(overview.AssetsPerAccount).Value);
        Assert.Equal(700m, overview.NetWorthSeries[^1].Value);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
    }
}