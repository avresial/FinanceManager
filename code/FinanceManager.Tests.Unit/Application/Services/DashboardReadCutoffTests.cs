using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
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
    public void OnlyTheCurrentDefaultWindowQualifiesForTheDayEndCutoff()
    {
        var now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
        var start = now.Date.AddDays(-30);
        var end = UtcDay.EndOf(now);
        Assert.True(DashboardReadCutoff.IsDefaultWindow(start, end, now));
        Assert.False(DashboardReadCutoff.IsDefaultWindow(start, now.AddHours(1), now));
        Assert.False(DashboardReadCutoff.IsDefaultWindow(start.AddDays(1), end, now));
        Assert.False(DashboardReadCutoff.IsDefaultWindow(start, end, now.AddDays(1)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DefaultAndCustomOverview_CacheDistinctResultsForIdenticalDates(bool firstDefault)
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
        foreach (var defaultRange in new[] { firstDefault, !firstDefault, firstDefault, !firstDefault })
        {
            var result = await service.GetOverview(1, currency.Id, start, end, TestContext.Current.CancellationToken, defaultRange);
            Assert.Equal(defaultRange ? 2 : 1, Assert.Single(result!.NetWorthSeries).Value);
        }
        worth.Verify(x => x.GetNetWorth(1, currency, start, end), Times.Exactly(2));
    }
}