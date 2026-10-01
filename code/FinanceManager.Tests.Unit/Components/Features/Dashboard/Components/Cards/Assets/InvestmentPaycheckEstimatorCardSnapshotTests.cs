using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public sealed class InvestmentPaycheckEstimatorCardSnapshotTests
{
    private const int _userId = 7;
    private const int _salaryMonths = 3;
    private const string _key = "investment-paycheck-source:7:0:3";

    [Fact]
    public async Task SnapshotHit_PaintsImmediatelyAndAlwaysFetches_EqualRenderedFactsDoNotWrite()
    {
        var source = Source(120_000m, 3, 5_000m);
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key))
            .ReturnsAsync(Snapshot(source));
        var handler = new DeferredEstimateHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.Started.Task);
        try
        {
            cut.WaitForAssertion(() => Assert.Contains("400.00", cut.Markup));
            Assert.Contains("5,000", cut.Markup);
            Assert.Contains("120000.00 PLN", cut.Markup);
            Assert.Contains("withdrawalRate=0.04", handler.Request!.RequestUri!.Query);
        }
        finally
        {
            handler.Complete(Estimate(120_000m, 3, 5_000m, asOfDate: DateTime.UtcNow.AddDays(1), rate: 0.08m));
        }
        await WaitFor(handler.Completed.Task);
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));
        var view = cut.FindComponent<InvestmentPaycheckEstimatorView>().Instance;
        view.OnPresetSelected(0.05m);
        Assert.Equal(500m, view.MonthlyPaycheck);

        snapshots.Verify(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key), Times.Once);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Never);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ChangedRenderedFacts_RefreshAndPersistWithStableScopedKey()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key))
            .ReturnsAsync(Snapshot(Source(120_000m, 3, 5_000m)));
        var handler = new DeferredEstimateHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.Started.Task);
        handler.Complete(Estimate(180_000m, 2, 6_000m));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("600.00", cut.Markup);
            Assert.Contains("180000.00 PLN", cut.Markup);
        });
        await WaitFor(handler.Completed.Task);
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));
        snapshots.Verify(service => service.SetAsync(_key, It.Is<InvestmentPaycheckSourceSnapshot>(snapshot =>
            snapshot.UserId == _userId
            && snapshot.CurrencyId == DefaultCurrency.PLN.Id
            && snapshot.SalaryMonths == _salaryMonths
            && snapshot.AsOfDateUtc.Kind == DateTimeKind.Utc
            && snapshot.Model == Source(180_000m, 2, 6_000m))), Times.Once);
    }

    [Fact]
    public async Task SuccessfulEmptySource_ReplacesPreviouslyPopulatedSnapshot()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key))
            .ReturnsAsync(Snapshot(Source(120_000m, 3, 5_000m)));
        var handler = new DeferredEstimateHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.Started.Task);
        handler.Complete(Estimate(0m, 0, null));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("0.00", cut.Markup);
            Assert.Contains("No salary history yet", cut.Markup);
            Assert.DoesNotContain("5,000 PLN salary", cut.Markup);
        });
        await WaitFor(handler.Completed.Task);
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));
        snapshots.Verify(service => service.SetAsync(_key, It.Is<InvestmentPaycheckSourceSnapshot>(snapshot =>
            snapshot.Model == Source(0m, 0, null))), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshFailureOrNullBody_DoesNotReplacePaintedSnapshot(bool nullBody)
    {
        var original = Source(120_000m, 3, 5_000m);
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key))
            .ReturnsAsync(Snapshot(original));
        var handler = new DeferredEstimateHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.Started.Task);
        if (nullBody) handler.CompleteNull();
        else handler.Fail();
        await WaitFor(handler.Completed.Task);
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));

        cut.WaitForAssertion(() => Assert.Contains("400.00", cut.Markup));
        Assert.Contains("120000.00 PLN", cut.Markup);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialNullOrHttpFailure_ShowsUnavailableInsteadOfSuccessfulZero(bool nullBody)
    {
        var snapshots = new Mock<ISnapshotService>();
        var handler = new DeferredEstimateHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.Started.Task);
        if (nullBody) handler.CompleteNull();
        else handler.Fail();
        await WaitFor(handler.Completed.Task);
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));

        cut.WaitForAssertion(() => Assert.Contains("Investment paycheck data is currently unavailable.", cut.Markup));
        Assert.DoesNotContain("0.00 PLN", cut.Markup);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task LoggedOutCard_DoesNotReadStorageOrFetchAndShowsUnavailable()
    {
        var snapshots = new Mock<ISnapshotService>();
        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync((UserSession?)null);
        var settings = new Mock<ISettingsService>();
        var handler = new DeferredEstimateHandler();
        await using var context = CreateContext(snapshots, handler, login, settings);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();

        cut.WaitForAssertion(() => Assert.Contains("Investment paycheck data is currently unavailable.", cut.Markup));
        Assert.Equal(0, handler.RequestCount);
        snapshots.Verify(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SnapshotReadFailure_FallsBackToFetch_AndWriteFailureKeepsFreshValueVisible()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key))
            .ThrowsAsync(new InvalidOperationException("read failed"));
        snapshots.Setup(service => service.SetAsync(_key, It.IsAny<InvestmentPaycheckSourceSnapshot>()))
            .ThrowsAsync(new InvalidOperationException("write failed"));
        var handler = new DeferredEstimateHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.Started.Task);
        handler.Complete(Estimate(120_000m, 3, 5_000m));
        await WaitFor(handler.Completed.Task);
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));

        cut.WaitForAssertion(() => Assert.Contains("400.00", cut.Markup));
        Assert.Contains("120000.00 PLN", cut.Markup);
        snapshots.Verify(service => service.RemoveAsync(_key), Times.Once);
        snapshots.Verify(service => service.SetAsync(_key, It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task UserAndCurrencyChangeWhileSnapshotReadIsPending_OnlyNewScopeCanPaintOrWrite()
    {
        const string newKey = "investment-paycheck-source:8:1:3";
        var oldRead = new TaskCompletionSource<InvestmentPaycheckSourceSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newRead = new TaskCompletionSource<InvestmentPaycheckSourceSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(It.IsAny<string>()))
            .Returns((string key) =>
            {
                if (key == _key)
                {
                    oldReadStarted.TrySetResult();
                    return oldRead.Task;
                }
                newReadStarted.TrySetResult();
                return newRead.Task;
            });
        var login = new Mock<ILoginService>();
        login.SetupSequence(service => service.GetLoggedUser())
            .ReturnsAsync(User(7))
            .ReturnsAsync(User(8));
        var settings = new Mock<ISettingsService>();
        settings.SetupSequence(service => service.GetCurrencyAsync())
            .ReturnsAsync(DefaultCurrency.PLN)
            .ReturnsAsync(DefaultCurrency.USD);
        var handler = new DeferredEstimateHandler();
        await using var context = CreateContext(snapshots, handler, login, settings);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(oldReadStarted.Task);
        var newerRun = cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvestmentPaycheckEstimatorCard.SalaryMonths)] = _salaryMonths,
            })));
        await WaitFor(newReadStarted.Task);
        newRead.SetResult(Snapshot(Source(999_000m, 3, 90_000m), userId: 7, currencyId: 0));
        await WaitFor(handler.Started.Task);
        Assert.DoesNotContain("999000.00", cut.Markup);
        Assert.DoesNotContain("90,000", cut.Markup);

        handler.Complete(Estimate(180_000m, 3, 6_000m));
        await WaitFor(handler.Completed.Task);
        await WaitFor(newerRun);
        oldRead.SetResult(Snapshot(Source(999_000m, 3, 90_000m), userId: 7, currencyId: 0));
        var coordinator = context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>();
        await WaitFor(coordinator.WaitCompleted(1));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("600.00", cut.Markup);
            Assert.Contains("180000.00 USD", cut.Markup);
        });
        snapshots.Verify(service => service.SetAsync(newKey, It.Is<InvestmentPaycheckSourceSnapshot>(snapshot => snapshot.UserId == 8 && snapshot.CurrencyId == 1)), Times.Once);
        snapshots.Verify(service => service.SetAsync(_key, It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Never);
        snapshots.Verify(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key), Times.Once);
        snapshots.Verify(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(newKey), Times.Once);
    }

    [Fact]
    public async Task CurrencyChange_ClearsPaintedOldCurrencyBeforeReadingNewSnapshot()
    {
        const string usdKey = "investment-paycheck-source:7:1:3";
        var usdRead = new TaskCompletionSource<InvestmentPaycheckSourceSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var usdReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key))
            .ReturnsAsync((InvestmentPaycheckSourceSnapshot?)null);
        snapshots.Setup(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(usdKey))
            .Returns(() =>
            {
                usdReadStarted.TrySetResult();
                return usdRead.Task;
            });
        var login = new Mock<ILoginService>();
        login.SetupSequence(service => service.GetLoggedUser())
            .ReturnsAsync(User(7))
            .ReturnsAsync(User(7));
        var settings = new Mock<ISettingsService>();
        settings.SetupSequence(service => service.GetCurrencyAsync())
            .ReturnsAsync(DefaultCurrency.PLN)
            .ReturnsAsync(DefaultCurrency.USD);
        var handler = new SequencedEstimateHandler();
        await using var context = CreateContext(snapshots, handler, login, settings);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.WaitStarted(1));
        handler.Complete(1, Estimate(120_000m, 3, 5_000m));
        await WaitFor(handler.WaitCompleted(1));
        var coordinator = context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>();
        await WaitFor(coordinator.WaitCompleted(1));
        cut.WaitForAssertion(() => Assert.Contains("120000.00 PLN", cut.Markup));

        var refresh = cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvestmentPaycheckEstimatorCard.SalaryMonths)] = _salaryMonths,
            })));
        await WaitFor(usdReadStarted.Task);
        Assert.DoesNotContain("120000.00", cut.Markup);

        usdRead.SetResult(null);
        await WaitFor(handler.WaitStarted(2));
        handler.Complete(2, Estimate(180_000m, 3, 6_000m));
        await WaitFor(handler.WaitCompleted(2));
        await WaitFor(refresh);
        await WaitFor(coordinator.WaitCompleted(2));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("600.00", cut.Markup);
            Assert.Contains("180000.00 USD", cut.Markup);
        });
        snapshots.Verify(service => service.SetAsync(usdKey, It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameScopeStorageMissOrReadFailureAndFreshFailure_PreservesPreviouslyDisplayedSource(bool readFails)
    {
        var snapshots = new Mock<ISnapshotService>();
        if (readFails)
            snapshots.SetupSequence(service => service.GetAsync<InvestmentPaycheckSourceSnapshot>(_key))
                .ReturnsAsync((InvestmentPaycheckSourceSnapshot?)null)
                .ThrowsAsync(new InvalidOperationException("read failed"));
        var handler = new SequencedEstimateHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.WaitStarted(1));
        handler.Complete(1, Estimate(120_000m, 3, 5_000m));
        await WaitFor(handler.WaitCompleted(1));
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));
        cut.WaitForAssertion(() => Assert.Contains("400.00", cut.Markup));

        var refresh = cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvestmentPaycheckEstimatorCard.SalaryMonths)] = _salaryMonths,
            })));
        await WaitFor(handler.WaitStarted(2));
        handler.Fail(2);
        await WaitFor(handler.WaitCompleted(2));
        await WaitFor(refresh);
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(2));

        Assert.Contains("400.00", cut.Markup);
        Assert.Contains("120000.00 PLN", cut.Markup);
        Assert.DoesNotContain("currently unavailable", cut.Markup);
        if (readFails)
            snapshots.Verify(service => service.RemoveAsync(_key), Times.Once);
    }

    [Fact]
    public async Task NewUserSettingsFailure_ClearsPriorUsersDataBeforeSettingsLoad()
    {
        var login = new Mock<ILoginService>();
        login.SetupSequence(service => service.GetLoggedUser())
            .ReturnsAsync(User(7))
            .ReturnsAsync(User(8));
        var settings = new Mock<ISettingsService>();
        settings.SetupSequence(service => service.GetCurrencyAsync())
            .ReturnsAsync(DefaultCurrency.PLN)
            .ThrowsAsync(new InvalidOperationException("settings unavailable"));
        var snapshots = new Mock<ISnapshotService>();
        var handler = new SequencedEstimateHandler();
        await using var context = CreateContext(snapshots, handler, login, settings);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.WaitStarted(1));
        handler.Complete(1, Estimate(120_000m, 3, 5_000m));
        await WaitFor(handler.WaitCompleted(1));
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));
        cut.WaitForAssertion(() => Assert.Contains("120000.00 PLN", cut.Markup));

        var refresh = cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvestmentPaycheckEstimatorCard.SalaryMonths)] = _salaryMonths,
            })));
        await WaitFor(refresh);

        Assert.Contains("Investment paycheck data is currently unavailable.", cut.Markup);
        Assert.DoesNotContain("120000.00 PLN", cut.Markup);
    }

    [Fact]
    public async Task SalaryMonthsChangeAndLoginFailure_ClearsPriorScopeBeforeLoginCompletes()
    {
        var login = new Mock<ILoginService>();
        login.SetupSequence(service => service.GetLoggedUser())
            .ReturnsAsync(User(7))
            .ThrowsAsync(new InvalidOperationException("login unavailable"));
        var snapshots = new Mock<ISnapshotService>();
        var handler = new SequencedEstimateHandler();
        await using var context = CreateContext(snapshots, handler, login);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.WaitStarted(1));
        handler.Complete(1, Estimate(120_000m, 3, 5_000m));
        await WaitFor(handler.WaitCompleted(1));
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));
        cut.WaitForAssertion(() => Assert.Contains("120000.00 PLN", cut.Markup));

        var refresh = cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvestmentPaycheckEstimatorCard.SalaryMonths)] = 6,
            })));
        await WaitFor(refresh);

        Assert.Contains("Investment paycheck data is currently unavailable.", cut.Markup);
        Assert.DoesNotContain("120000.00 PLN", cut.Markup);
        handler.VerifyStartedCount(1);
    }

    [Fact]
    public async Task SupersededSalaryMonthsRun_CannotWriteAfterNewerContextCompletes()
    {
        var snapshots = new Mock<ISnapshotService>();
        var handler = new PerMonthsEstimateHandler();
        await using var context = CreateContext(snapshots, handler);

        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        await WaitFor(handler.WaitStarted(_salaryMonths));
        var newerRun = cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvestmentPaycheckEstimatorCard.SalaryMonths)] = 6,
            })));
        await WaitFor(handler.WaitStarted(6));
        handler.Complete(6, Estimate(180_000m, 6, 6_000m, salaryMonthsRequested: 6));
        cut.WaitForAssertion(() => Assert.Contains("600.00", cut.Markup));
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(2));

        handler.Complete(_salaryMonths, Estimate(90_000m, 3, 3_000m));
        await WaitFor(handler.WaitCompleted(_salaryMonths));
        await WaitFor(newerRun);
        await WaitFor(context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>().WaitCompleted(1));
        Assert.Contains("600.00", cut.Markup);
        snapshots.Verify(service => service.SetAsync("investment-paycheck-source:7:0:6", It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Once);
        snapshots.Verify(service => service.SetAsync(_key, It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task DisposingComponents_InvalidatesPendingFetchBeforeItCanPersist()
    {
        var snapshots = new Mock<ISnapshotService>();
        var handler = new DeferredEstimateHandler();
        var context = CreateContext(snapshots, handler);
        var cut = context.Render<InvestmentPaycheckEstimatorCard>();
        var coordinator = context.Services.GetRequiredService<TrackingSnapshotRefreshCoordinator>();
        await WaitFor(handler.Started.Task);

        var disposal = context.DisposeComponentsAsync();
        handler.Complete(Estimate(120_000m, 3, 5_000m));
        await WaitFor(disposal);
        await WaitFor(handler.Completed.Task);
        await WaitFor(coordinator.WaitCompleted(1));

        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentPaycheckSourceSnapshot>()), Times.Never);
        await context.DisposeAsync();
    }

    private static Task WaitFor(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

    private static BunitContext CreateContext(Mock<ISnapshotService> snapshots, HttpMessageHandler handler,
        Mock<ILoginService>? suppliedLogin = null, Mock<ISettingsService>? suppliedSettings = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var settings = suppliedSettings ?? new Mock<ISettingsService>();
        if (suppliedSettings is null)
            settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var login = suppliedLogin ?? new Mock<ILoginService>();
        if (suppliedLogin is null)
            login.Setup(service => service.GetLoggedUser()).ReturnsAsync(User(_userId));
        context.Services.AddSingleton(login.Object);
        var coordinator = new TrackingSnapshotRefreshCoordinator(snapshots.Object);
        context.Services.AddSingleton(coordinator);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(coordinator);
        context.Services.AddSingleton(new AssetsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        return context;
    }

    private static InvestmentPaycheckSourceSnapshot Snapshot(InvestmentPaycheckSourceModel source) => new()
    {
        UserId = _userId,
        CurrencyId = DefaultCurrency.PLN.Id,
        SalaryMonths = _salaryMonths,
        AsOfDateUtc = DateTime.UtcNow.AddDays(-1),
        Model = source,
    };

    private static InvestmentPaycheckSourceSnapshot Snapshot(InvestmentPaycheckSourceModel source, int userId, int currencyId) => new()
    {
        UserId = userId,
        CurrencyId = currencyId,
        SalaryMonths = _salaryMonths,
        AsOfDateUtc = DateTime.UtcNow.AddDays(-1),
        Model = source,
    };

    private static UserSession User(int userId) => new()
    {
        UserId = userId,
        UserName = userId == 7 ? "guest" : $"user{userId}",
        Password = string.Empty,
        UserRole = UserRole.User,
    };

    private static InvestmentPaycheckSourceModel Source(decimal assets, int monthsUsed, decimal? averageSalary) => new(
        assets,
        _salaryMonths,
        monthsUsed,
        averageSalary);

    private static InvestmentPaycheckEstimate Estimate(decimal assets, int monthsUsed, decimal? averageSalary,
        DateTime? asOfDate = null, decimal rate = 0.05m, int salaryMonthsRequested = _salaryMonths) => new()
        {
            AsOfDate = asOfDate ?? DateTime.UtcNow,
            AnnualWithdrawalRate = rate,
            InvestableAssetsValue = assets,
            SustainableMonthlyPaycheck = Math.Round(assets * rate / 12m, 2),
            SalaryMonthsRequested = salaryMonthsRequested,
            SalaryMonthsUsed = monthsUsed,
            AverageMonthlySalary = averageSalary,
            IncomeReplacementRatio = null,
        };

    private sealed class DeferredEstimateHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HttpRequestMessage? Request { get; private set; }
        public int RequestCount => _requestCount;

        public void Complete(InvestmentPaycheckEstimate estimate) => _response.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(estimate),
        });

        public void CompleteNull() => _response.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
        });

        public void Fail() => _response.TrySetException(new HttpRequestException("test failure"));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            Request = request;
            Started.TrySetResult();
            try { return await _response.Task; }
            finally { Completed.TrySetResult(); }
        }
    }

    private sealed class PerMonthsEstimateHandler : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<int, TaskCompletionSource<HttpResponseMessage>> _responses = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _started = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _completed = new();

        public Task WaitStarted(int months) => _started.GetOrAdd(months, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        public Task WaitCompleted(int months) => _completed.GetOrAdd(months, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

        public void Complete(int months, InvestmentPaycheckEstimate estimate) => _responses
            .GetOrAdd(months, _ => new(TaskCreationOptions.RunContinuationsAsynchronously))
            .TrySetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(estimate) });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var months = int.Parse(query["salaryMonths"]!, System.Globalization.CultureInfo.InvariantCulture);
            _started.GetOrAdd(months, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            var response = await _responses.GetOrAdd(months, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            _completed.GetOrAdd(months, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            return response;
        }
    }

    private sealed class SequencedEstimateHandler : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<int, TaskCompletionSource<HttpResponseMessage>> _responses = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _started = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _completed = new();
        private int _requestCount;

        public Task WaitStarted(int request) => _started.GetOrAdd(request, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        public Task WaitCompleted(int request) => _completed.GetOrAdd(request, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        public void Complete(int request, InvestmentPaycheckEstimate estimate) => _responses
            .GetOrAdd(request, _ => new(TaskCreationOptions.RunContinuationsAsynchronously))
            .TrySetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(estimate) });
        public void Fail(int request) => _responses
            .GetOrAdd(request, _ => new(TaskCreationOptions.RunContinuationsAsynchronously))
            .TrySetException(new HttpRequestException("test failure"));
        public void VerifyStartedCount(int count) => Assert.Equal(count, _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _requestCount);
            _started.GetOrAdd(call, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            try { return await _responses.GetOrAdd(call, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task; }
            finally { _completed.GetOrAdd(call, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(); }
        }
    }

    private sealed class TrackingSnapshotRefreshCoordinator(ISnapshotService snapshots) : ISnapshotRefreshCoordinator
    {
        private readonly SnapshotRefreshCoordinator _inner = new(snapshots, NullLogger<SnapshotRefreshCoordinator>.Instance);
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _completed = new();
        private int _runCount;

        public Task WaitCompleted(int run) => _completed.GetOrAdd(run, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

        public async Task<FinanceManager.Components.Shared.Models.SnapshotRefreshResult<TModel>> RunAsync<TSnapshot, TModel>(
            FinanceManager.Components.Shared.Services.SnapshotRefreshRequest<TSnapshot, TModel> request)
            where TSnapshot : FinanceManager.Components.Shared.Models.SnapshotBase
            where TModel : class
        {
            var run = Interlocked.Increment(ref _runCount);
            try { return await _inner.RunAsync(request); }
            finally { _completed.GetOrAdd(run, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(); }
        }
    }
}