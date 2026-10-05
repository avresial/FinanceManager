using Bunit;
using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.Components;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Components.Features.Alerts.Models;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Alerts;

[Trait("Category", "Unit")]
public sealed class AlertsPageTests
{
    private static readonly Guid _alertId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SnapshotHit_PaintsBeforeEvaluationAndKeepsItOnFailure()
    {
        var handler = new AlertsHandler { DelayEvaluation = true };
        var snapshots = new SnapshotStore { Snapshot = Snapshot("Cached alert") };
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.Contains("Cached alert", cut.Markup));
        Assert.DoesNotContain("mud-skeleton", cut.Markup);
        Assert.Equal(1, handler.GetCount);
        Assert.Equal(1, handler.EvaluateCount);

        handler.CompleteEvaluation(HttpStatusCode.ServiceUnavailable);
        cut.WaitForAssertion(() => Assert.Contains("Unable to refresh alerts", cut.Markup));
        Assert.Contains("Cached alert", cut.Markup);
        Assert.Equal("Cached alert", snapshots.Snapshot!.Alerts[0].Title);
    }

    [Fact]
    public async Task Form_OffersAllAccountsAndUserAccounts_InsteadOfFreeTextId()
    {
        var handler = new AlertsHandler();
        await using var context = CreateContext(handler, new SnapshotStore());
        var popovers = context.Render<MudPopoverProvider>();
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.DoesNotContain("Account id", cut.Markup));
        cut.WaitForAssertion(() => Assert.Contains("All accounts", cut.Markup));
        cut.FindAll(".mud-select").First(select => select.TextContent.Contains("All accounts")).QuerySelector(".mud-input-control")!.MouseDown();

        cut.WaitForAssertion(() =>
        {
            var items = popovers.FindAll(".mud-list-item").Select(item => item.TextContent.Trim()).ToList();
            Assert.Contains("Everyday", items);
            Assert.Contains("Savings", items);
        });
    }

    [Fact]
    public async Task Form_ThresholdAdornmentShowsCurrency_NotLiteralValue()
    {
        await using var context = CreateContext(new AlertsHandler(), new SnapshotStore());
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".mud-input-adornment-end"), adornment => adornment.TextContent.Trim() == DefaultCurrency.PLN.ShortName));
        Assert.DoesNotContain(">value<", cut.Markup);
    }

    [Fact]
    public async Task SnapshotMiss_ShowsSkeletonUntilFreshResultsArrive()
    {
        var handler = new AlertsHandler { DelayEvaluation = true };
        var snapshots = new SnapshotStore();
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.Contains("mud-skeleton", cut.Markup));
        handler.CompleteEvaluation(HttpStatusCode.OK);
        cut.WaitForAssertion(() => Assert.Contains("Fresh alert", cut.Markup));
        Assert.DoesNotContain("mud-skeleton", cut.Markup);
        cut.WaitForAssertion(() => Assert.Equal("Fresh alert", snapshots.Snapshot!.Alerts[0].Title));
        Assert.Equal("alerts-page:7", snapshots.LastKey);
    }

    [Fact]
    public async Task AnotherUsersSnapshot_IsRejected()
    {
        var handler = new AlertsHandler { DelayEvaluation = true };
        var foreignSnapshot = Snapshot("Other user's alert");
        foreignSnapshot.UserId = 8;
        var snapshots = new SnapshotStore { Snapshot = foreignSnapshot };
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.Contains("mud-skeleton", cut.Markup));
        Assert.DoesNotContain("Other user's alert", cut.Markup);
        handler.CompleteEvaluation(HttpStatusCode.OK);
        cut.WaitForAssertion(() => Assert.Equal(7, snapshots.Snapshot!.UserId));
    }

    [Theory]
    [InlineData("Fresh alert", 1)]
    [InlineData("Cached alert", 0)]
    public async Task Refresh_OnlyPersistsChangedRenderedContent(string freshTitle, int expectedWrites)
    {
        var handler = new AlertsHandler { AlertTitle = freshTitle };
        var snapshots = new SnapshotStore { Snapshot = Snapshot("Cached alert") };
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluateCount));
        cut.WaitForAssertion(() => Assert.Contains(freshTitle, cut.Markup));
        Assert.Equal(expectedWrites, snapshots.WriteCount);
    }

    [Fact]
    public async Task SuccessfulDelete_InvalidatesOldSnapshotAndPersistsRefreshedEmptyState()
    {
        var handler = new AlertsHandler();
        var snapshots = new SnapshotStore { Snapshot = Snapshot("Cached alert") };
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Fresh alert", cut.Markup));

        cut.Find("button[aria-label='Delete Fresh alert']").Click();
        cut.WaitForAssertion(() => Assert.Contains("No alerts yet", cut.Markup));
        Assert.True(snapshots.RemoveCount > 0);
        Assert.Empty(snapshots.Snapshot!.Alerts);
    }

    [Fact]
    public async Task EvaluateNow_PersistsLatestRenderedResults()
    {
        var handler = new AlertsHandler();
        var snapshots = new SnapshotStore { Snapshot = Snapshot("Cached alert") };
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Equal("Fresh alert", snapshots.Snapshot!.Alerts[0].Title));

        handler.AlertTitle = "Updated alert";
        cut.FindAll("button").Single(button => button.TextContent.Contains("Evaluate now", StringComparison.OrdinalIgnoreCase)).Click();

        cut.WaitForAssertion(() => Assert.Equal("Updated alert", snapshots.Snapshot!.Alerts[0].Title));
        Assert.Contains("Updated alert", cut.Markup);
        Assert.Equal(2, handler.EvaluateCount);
    }

    [Fact]
    public async Task Mutation_SupersedesOlderEvaluationResponse()
    {
        var handler = new AlertsHandler { DelayFirstEvaluation = true };
        var snapshots = new SnapshotStore { Snapshot = Snapshot("Cached alert") };
        await using var context = CreateContext(handler, snapshots);
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluateCount));

        cut.Find("button[aria-label='Delete Cached alert']").Click();
        cut.WaitForAssertion(() => Assert.Contains("No alerts yet", cut.Markup));
        handler.CompleteEvaluation(HttpStatusCode.OK);

        cut.WaitForAssertion(() => Assert.Empty(snapshots.Snapshot!.Alerts));
        Assert.DoesNotContain("Cached alert", cut.Markup);
    }

    private static AlertsPageSnapshot Snapshot(string title) => new()
    {
        UserId = 7,
        Alerts = [Alert(title)],
        Outcomes = [Outcome()],
    };

    private static FinancialAlertDto Alert(string title) => new(
        _alertId, 7, title, AlertType.AccountBalance, true,
        AlertComparisonOperator.GreaterThan, 100m, AlertEvaluationPeriod.CurrentMonth,
        null, null, null, null, null, AlertTriggerStatus.Healthy,
        null, null, new DateTime(2026, 9, 1), null);

    private static AlertEvaluationOutcome Outcome() => new(
        _alertId, "Fresh alert", AlertType.AccountBalance, AlertTriggerStatus.Healthy,
        false, null, false, DeDuplicationReason.None, 50m, 100m,
        AlertComparisonOperator.GreaterThan, "stable", "Healthy",
        DateTime.UtcNow, new Dictionary<string, string>());

    private static BunitContext CreateContext(AlertsHandler handler, SnapshotStore snapshots)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 7,
            UserName = "guest",
            Password = string.Empty,
            UserRole = UserRole.User
        });
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton<ISnapshotService>(snapshots);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(
            new SnapshotRefreshCoordinator(snapshots, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(new FinancialAlertsHttpClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        }));
        context.Services.AddSingleton(new CurrencyAccountHttpClient(new HttpClient(new AccountsHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        }));
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        return context;
    }

    private sealed class AccountsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<AvailableAccount> { new(11, "Everyday"), new(12, "Savings") })
            });
    }

    private sealed class SnapshotStore : ISnapshotService
    {
        public AlertsPageSnapshot? Snapshot { get; set; }
        public int WriteCount { get; private set; }
        public int RemoveCount { get; private set; }
        public string? LastKey { get; private set; }

        public Task<T?> GetAsync<T>(string key) where T : FinanceManager.Components.Shared.Models.SnapshotBase
        {
            LastKey = key;
            return Task.FromResult(Snapshot as T);
        }

        public Task SetAsync<T>(string key, T snapshot) where T : FinanceManager.Components.Shared.Models.SnapshotBase
        {
            Snapshot = (AlertsPageSnapshot)(object)snapshot;
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            Snapshot = null;
            RemoveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class AlertsHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpStatusCode> _evaluationResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DelayEvaluation { get; init; }
        public string AlertTitle { get; set; } = "Fresh alert";
        public bool DelayFirstEvaluation { get; init; }
        public int GetCount { get; private set; }
        public int EvaluateCount { get; private set; }
        private bool _deleted;

        public void CompleteEvaluation(HttpStatusCode status) => _evaluationResult.TrySetResult(status);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Delete)
            {
                _deleted = true;
                return new(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Get)
            {
                GetCount++;
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(_deleted ? new List<FinancialAlertDto>() : [Alert(AlertTitle)])
                };
            }

            EvaluateCount++;
            var status = DelayEvaluation || DelayFirstEvaluation && EvaluateCount == 1
                ? await _evaluationResult.Task.WaitAsync(cancellationToken)
                : HttpStatusCode.OK;
            return new(status)
            {
                Content = status == HttpStatusCode.OK
                    ? JsonContent.Create(_deleted ? new List<AlertEvaluationOutcome>() : [Outcome()])
                    : new StringContent("unavailable")
            };
        }
    }
}