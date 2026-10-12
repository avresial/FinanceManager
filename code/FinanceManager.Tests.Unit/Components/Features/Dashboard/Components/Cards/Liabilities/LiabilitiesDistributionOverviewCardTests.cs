using Bunit;
using Bunit.TestDoubles;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Liabilities;

[Trait("Category", "Unit")]
public class LiabilitiesDistributionOverviewCardTests
{
    [Fact]
    public async Task PreparedModel_SupersedesPendingSelfLoad_AndClearsRefreshIndicator()
    {
        await using var context = new BunitContext();
        context.ComponentFactories.AddStub<LiabilitiesDistributionOverviewCardView>();
        context.Services.AddLogging();
        var loginResult = new TaskCompletionSource<UserSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).Returns(loginResult.Task);
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(new LiabilitiesHttpClient(new HttpClient()));
        context.Services.AddSingleton(new LiabilitiesSnapshotStore(new SnapshotRefreshCoordinator(
            Mock.Of<ISnapshotService>(), NullLogger<SnapshotRefreshCoordinator>.Instance)));
        var refreshing = new List<bool>();
        var cut = context.Render<LiabilitiesDistributionOverviewCard>(p => p
            .Add(x => x.StartDateTime, new DateTime(2026, 9, 1))
            .Add(x => x.RefreshingChanged, value => refreshing.Add(value)));
        Assert.Equal([true], refreshing);

        var model = new DistributionCardModel([new NameValueResult("Loan", -50)], []);
        cut.Render(p => p.Add(x => x.Model, model));
        Assert.Equal([true, false], refreshing);
        loginResult.SetResult(null);
        await cut.InvokeAsync(() => Task.CompletedTask);
        var view = cut.FindComponent<Stub<LiabilitiesDistributionOverviewCardView>>();
        Assert.Equal(50, view.Instance.Parameters.Get(x => x.TypeData).Single().Value);
        Assert.Equal([true, false], refreshing);
    }
}