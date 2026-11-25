using Bunit;
using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.Components;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Components.Features.Alerts.Models;
using FinanceManager.Components.Features.Currencies.HttpClients;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Currencies.Entities;
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
    public async Task EmptyStateCreateAction_FocusesExistingAlertNameWithoutSubmitting()
    {
        var handler = new AlertsHandler();
        await using var context = CreateContext(handler, new SnapshotStore());
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Fresh alert", cut.Markup));
        cut.Find("button[aria-label='Delete Fresh alert']").Click();
        cut.WaitForAssertion(() => Assert.Contains("No alerts yet", cut.Markup));
        var evaluations = handler.EvaluateCount;
        var alertName = cut.FindComponents<MudTextField<string>>().Single(field => field.Instance.Label == "Alert name");
        var expectedTarget = alertName.Instance.InputReference!.ElementReference;
        var invocationCount = context.JSInterop.Invocations.Count;

        cut.Find("[data-testid=alerts-empty-create]").Click();

        var focus = Assert.Single(context.JSInterop.Invocations.Skip(invocationCount), invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");
        Assert.Equal(expectedTarget, Assert.IsType<Microsoft.AspNetCore.Components.ElementReference>(focus.Arguments[0]));
        Assert.Equal(evaluations, handler.EvaluateCount);
        Assert.Single(cut.FindAll("label"), label => label.TextContent.Contains("Alert name", StringComparison.Ordinal));
    }

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
    public async Task Form_OffersCurrenciesInsteadOfAccounts_DefaultingToThePreferredOne()
    {
        var handler = new AlertsHandler();
        await using var context = CreateContext(handler, new SnapshotStore());
        var popovers = context.Render<MudPopoverProvider>();
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='alert-currency-select']")));
        Assert.DoesNotContain("All accounts", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='alert-account-select']"));
        cut.WaitForAssertion(() => Assert.Equal("0", SelectedCurrency(cut)));

        OpenCurrencySelect(cut);

        cut.WaitForAssertion(() =>
        {
            var items = popovers.FindAll(".mud-list-item").Select(item => item.TextContent.Trim()).ToList();
            // EUR exists in the catalog but the user has no EUR transactions.
            Assert.Equal(["PLN", "USD"], items);
        });
    }

    [Fact]
    public async Task Form_ThresholdAdornmentFollowsTheSelectedCurrency()
    {
        var handler = new AlertsHandler();
        await using var context = CreateContext(handler, new SnapshotStore());
        var popovers = context.Render<MudPopoverProvider>();
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Equal("0", SelectedCurrency(cut)));

        OpenCurrencySelect(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(popovers.FindAll(".mud-list-item")));
        popovers.FindAll(".mud-list-item").Single(item => item.TextContent.Trim() == "USD").Click();

        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".mud-input-adornment-end"), adornment => adornment.TextContent.Trim() == "USD"));
    }

    [Fact]
    public async Task MigratedAlertWithoutCurrency_SaysWhatToDoAndCannotBeEnabledFromTheSwitch()
    {
        var handler = new AlertsHandler { Migrated = true };
        await using var context = CreateContext(handler, new SnapshotStore());
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.Contains("Migrated alert", cut.Markup));
        Assert.Contains("(currency required)", cut.Markup);
        Assert.True(cut.Find("input[aria-label='Enable Migrated alert']").HasAttribute("disabled"));
        // A neutral "Disabled" chip would read as a finished state; this one is waiting for the user.
        var status = cut.Find("td[data-label='Status'] .mud-chip");
        Assert.Equal("Needs currency", status.TextContent.Trim());
        Assert.Contains("mud-chip-color-warning", status.ClassName);
        Assert.DoesNotContain("0.00", cut.Find("td[data-label='Observed']").TextContent);
        Assert.Contains("Choose a currency to turn it back on", cut.Find("td[data-label='Observed']").TextContent);
        var tooltip = cut.FindComponents<MudTooltip>().Single().Instance;
        Assert.False(tooltip.Disabled);
        Assert.Contains("Choose a currency first", tooltip.Text);
    }

    [Fact]
    public async Task ChooseCurrency_OpensTheEditorAndSavingTurnsTheAlertOnByDefault()
    {
        var handler = new AlertsHandler { Migrated = true };
        await using var context = CreateContext(handler, new SnapshotStore());
        var popovers = context.Render<MudPopoverProvider>();
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Migrated alert", cut.Markup));

        cut.Find("[data-testid='alert-choose-currency']").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid='alert-needs-currency-hint']"));
        Assert.True(string.IsNullOrEmpty(SelectedCurrency(cut)));
        // No currency chosen yet, so the threshold shows no (wrong) code.
        Assert.DoesNotContain(cut.FindAll(".mud-input-adornment-end"), adornment => adornment.TextContent.Trim().Length == 3);
        Assert.NotEmpty(cut.FindAll("[data-testid='alert-enable-on-save']"));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Save changes")).Click();
        cut.WaitForAssertion(() => Assert.Contains("Select a currency for this alert.", cut.Markup));
        Assert.Null(handler.LastUpdate);

        OpenCurrencySelect(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(popovers.FindAll(".mud-list-item")));
        popovers.FindAll(".mud-list-item").Single(item => item.TextContent.Trim() == "USD").Click();
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".mud-input-adornment-end"), adornment => adornment.TextContent.Trim() == "USD"));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save changes")).Click();

        cut.WaitForAssertion(() => Assert.NotNull(handler.LastUpdate));
        Assert.Contains("\"currencyId\":2", handler.LastUpdate);
        Assert.Contains("\"isEnabled\":true", handler.LastUpdate);
    }

    [Fact]
    public async Task ChooseCurrency_LetsTheUserSaveTheCurrencyWithoutTurningTheAlertOn()
    {
        var handler = new AlertsHandler { Migrated = true };
        await using var context = CreateContext(handler, new SnapshotStore());
        var popovers = context.Render<MudPopoverProvider>();
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Migrated alert", cut.Markup));
        cut.Find("[data-testid='alert-choose-currency']").Click();
        OpenCurrencySelect(cut);
        cut.WaitForAssertion(() => Assert.NotEmpty(popovers.FindAll(".mud-list-item")));
        popovers.FindAll(".mud-list-item").Single(item => item.TextContent.Trim() == "USD").Click();

        var enableOnSave = cut.FindComponent<MudCheckBox<bool>>();
        await cut.InvokeAsync(() => enableOnSave.Instance.ValueChanged.InvokeAsync(false));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save changes")).Click();

        cut.WaitForAssertion(() => Assert.NotNull(handler.LastUpdate));
        Assert.Contains("\"isEnabled\":false", handler.LastUpdate);
    }

    [Fact]
    public async Task AlertWhoseStoredCurrencyIsNotInTheCatalog_IsTreatedAsNeedingACurrency()
    {
        var handler = new AlertsHandler { StoredCurrencyId = 999, StartDisabled = true };
        await using var context = CreateContext(handler, new SnapshotStore());
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.Equal("Needs currency", cut.Find("td[data-label='Status'] .mud-chip").TextContent.Trim()));
        Assert.True(cut.Find("input[aria-label='Enable Fresh alert']").HasAttribute("disabled"));

        cut.Find("button[aria-label='Edit Fresh alert']").Click();

        Assert.True(string.IsNullOrEmpty(SelectedCurrency(cut)));
    }

    [Fact]
    public async Task EditingAnAlertInACurrencyWithoutTransactions_StillOffersThatCurrency()
    {
        var handler = new AlertsHandler { StoredCurrencyId = 1 };
        await using var context = CreateContext(handler, new SnapshotStore());
        var popovers = context.Render<MudPopoverProvider>();
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Fresh alert", cut.Markup));

        cut.Find("button[aria-label='Edit Fresh alert']").Click();
        OpenCurrencySelect(cut);

        cut.WaitForAssertion(() => Assert.Equal(["EUR", "PLN", "USD"], popovers.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())));
    }

    [Fact]
    public async Task CurrencyOptions_FallBackToTheWholeCatalogWhenTheUsersCurrenciesAreUnknown()
    {
        await using var context = CreateContext(new AlertsHandler(), new SnapshotStore(), ownedCurrencies: HttpStatusCode.ServiceUnavailable);
        var popovers = context.Render<MudPopoverProvider>();
        var cut = context.Render<AlertsPage>();
        cut.WaitForAssertion(() => Assert.Equal("0", SelectedCurrency(cut)));

        OpenCurrencySelect(cut);

        cut.WaitForAssertion(() => Assert.Equal(["EUR", "PLN", "USD"], popovers.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())));
    }

    [Fact]
    public async Task CurrencyHelperText_DoesNotClaimThatAmountsAreSummed()
    {
        await using var context = CreateContext(new AlertsHandler(), new SnapshotStore());
        var cut = context.Render<AlertsPage>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='alert-currency-select']")));

        Assert.Contains("Only transactions in this currency are checked.", cut.Markup);
        Assert.DoesNotContain("summed", cut.Markup);
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
        Assert.Equal("alerts-page:v2:7", snapshots.LastKey);
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

    // The test id lands on the select's hidden input, whose value is the selected currency id (missing when none is chosen).
    private static string? SelectedCurrency(IRenderedComponent<AlertsPage> cut) =>
        cut.Find("[data-testid='alert-currency-select']").GetAttribute("value");

    private static void OpenCurrencySelect(IRenderedComponent<AlertsPage> cut) =>
        cut.Find("[data-testid='alert-currency-select']").Closest(".mud-input-control")!.MouseDown();

    private static AlertsPageSnapshot Snapshot(string title) => new()
    {
        UserId = 7,
        Alerts = [Alert(title)],
        Outcomes = [Outcome()],
    };

    private static FinancialAlertDto Alert(string title, bool migrated = false, int? currencyId = 0, bool disabled = false) => new(
        _alertId, 7, title, AlertType.AccountBalance, !migrated && !disabled,
        AlertComparisonOperator.GreaterThan, 100m, AlertEvaluationPeriod.CurrentMonth,
        migrated ? null : currencyId, null, null, null, null, AlertTriggerStatus.Healthy,
        null, null, new DateTime(2026, 9, 1), null);

    private static AlertEvaluationOutcome Outcome(bool migrated = false) => new(
        _alertId, migrated ? "Migrated alert" : "Fresh alert", AlertType.AccountBalance,
        migrated ? AlertTriggerStatus.Disabled : AlertTriggerStatus.Healthy,
        false, null, false, DeDuplicationReason.None, 50m, 100m,
        AlertComparisonOperator.GreaterThan, "stable", "Healthy",
        DateTime.UtcNow, new Dictionary<string, string>());

    private static BunitContext CreateContext(AlertsHandler handler, SnapshotStore snapshots, HttpStatusCode ownedCurrencies = HttpStatusCode.OK)
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
        context.Services.AddSingleton(new CurrencyHttpClient(new HttpClient(new CurrenciesHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        }));
        context.Services.AddSingleton(new TransactionLogHttpClient(new HttpClient(new OwnedCurrenciesHandler(ownedCurrencies))
        {
            BaseAddress = new Uri("http://localhost/")
        }));
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        return context;
    }

    // The currency codes of the user's own transactions: PLN and USD, but none in EUR.
    private sealed class OwnedCurrenciesHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = status == HttpStatusCode.OK ? JsonContent.Create(new List<string> { "PLN", "USD" }) : new StringContent("unavailable")
            });
    }

    private sealed class CurrenciesHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<Currency> { new(2, "USD", "$"), DefaultCurrency.PLN, new(1, "EUR", "€") })
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
        public bool Migrated { get; init; }
        public int? StoredCurrencyId { get; init; } = 0;
        public bool StartDisabled { get; init; }
        public string? LastUpdate { get; private set; }
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

            if (request.Method == HttpMethod.Put)
            {
                LastUpdate = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Alert(AlertTitle)) };
            }

            if (request.Method == HttpMethod.Get)
            {
                GetCount++;
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(_deleted ? new List<FinancialAlertDto>() : [Alert(Migrated ? "Migrated alert" : AlertTitle, Migrated, StoredCurrencyId, StartDisabled)])
                };
            }

            EvaluateCount++;
            var status = DelayEvaluation || DelayFirstEvaluation && EvaluateCount == 1
                ? await _evaluationResult.Task.WaitAsync(cancellationToken)
                : HttpStatusCode.OK;
            return new(status)
            {
                Content = status == HttpStatusCode.OK
                    ? JsonContent.Create(_deleted ? new List<AlertEvaluationOutcome>() : [Outcome(Migrated)])
                    : new StringContent("unavailable")
            };
        }
    }
}