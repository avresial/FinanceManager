using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Shared;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class DashboardReadCutoffTests
{
    [Fact]
    public async Task ConcurrentRead_UsesDefaultCutoffOnlyInItsOwnExecutionContext()
    {
        var now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
        var end = UtcDay.EndOf(now);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<DateTime> ReadDefault()
        {
            using var scope = DashboardReadCutoff.Enter(end);
            await resume.Task;
            return DashboardReadCutoff.Clamp(end.AddDays(1), now);
        }

        var pending = ReadDefault();
        Assert.Equal(now, DashboardReadCutoff.Clamp(end, now));
        resume.SetResult();
        Assert.Equal(end, await pending);
        Assert.Equal(now, DashboardReadCutoff.Clamp(end, now));
    }

    [Fact]
    public async Task RepeatedCalendarRange_ReusesOverviewComputedThroughEndOfUtcDay()
    {
        var now = DateTime.UtcNow;
        var start = now.Date.AddDays(-30);
        var end = UtcDay.EndOf(now);
        var currency = DefaultCurrency.PLN;
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(x => x.GetCurrency(currency.Id, It.IsAny<CancellationToken>())).ReturnsAsync(currency);
        var worth = new Mock<INetWorthService>();
        worth.Setup(x => x.GetNetWorth(1, currency, start, end)).ReturnsAsync(() =>
            new Dictionary<DateTime, decimal> { [start] = DashboardReadCutoff.Clamp(end, now) == end ? 2 : 1 });
        var liabilities = new Mock<ILiabilitiesService>();
        liabilities.Setup(x => x.GetEndLiabilitiesPerAccount(1, start, end)).Returns(Array.Empty<NameValueResult>().ToAsyncEnumerable());
        liabilities.Setup(x => x.GetEndLiabilitiesPerType(1, start, end)).Returns(Array.Empty<NameValueResult>().ToAsyncEnumerable());
        var assets = new Mock<IAssetsService>();
        assets.Setup(x => x.GetEndAssetsDistribution(1, currency, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetsDistributionResult([], []));
        var inner = new DashboardQueryService(worth.Object, Mock.Of<ILabelsValueService>(), Mock.Of<IBalanceService>(),
            assets.Object, liabilities.Object, Mock.Of<IExpenseDistributionService>(), currencies.Object);
        var services = new ServiceCollection();
        services.AddHybridCache();
        using var provider = services.BuildServiceProvider();
        var service = new CachingDashboardQueryService(inner, provider.GetRequiredService<HybridCache>());
        for (var visit = 0; visit < 2; visit++)
        {
            var result = await service.GetOverview(1, currency.Id, start, end, TestContext.Current.CancellationToken);
            Assert.Equal(2m, Assert.Single(result!.NetWorthSeries).Value);
        }
        worth.Verify(x => x.GetNetWorth(1, currency, start, end), Times.Once);
    }
}