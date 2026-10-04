using Bunit;
using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.Components;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public sealed class FinancialAlertsCardTests
{
    private const string _key = "financial-alerts:1";
    private static readonly Guid _storedAlertId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private readonly Mock<ISnapshotService> _snapshots = new();

    [Fact]
    public async Task Header_PlacesAlertsAndManageActionOnOneHeaderRow_WithIconOnlyButtonAndTooltip()
    {
        var alert1 = CreateAlertDto(title: "High spending");
        var alert2 = CreateAlertDto(title: "Low balance");
        var triggered = CreateOutcome(alertId: alert1.Id, title: "High spending", isTriggered: true);

        await using var context = CreateContext(outcomes: [triggered], alerts: [alert1, alert2]);
        var cut = context.Render<FinancialAlertsCard>();

        var headerRow = cut.Find(".fm-card-info-header");
        Assert.NotNull(headerRow);

        var titleInHeader = headerRow.QuerySelector(".mud-typography-h5");
        Assert.NotNull(titleInHeader);
        Assert.Equal("Alerts", titleInHeader.TextContent.Trim());

        var manageButton = headerRow.QuerySelector("a[href='Alerts']");
        Assert.NotNull(manageButton);
        Assert.Equal("Manage alerts", manageButton.GetAttribute("aria-label"));

        // Manage button should be icon-only: text content must not contain "Manage alerts"
        Assert.DoesNotContain("Manage alerts", manageButton.TextContent);

        // Tooltip wrapper should provide the "Manage alerts" hint
        var tooltip = headerRow.QuerySelector(".mud-tooltip-root");
        Assert.NotNull(tooltip);

        // Configured / triggered summary must be present below the header row
        var summary = cut.Find(".mud-typography-subtitle1.text-muted");
        Assert.NotNull(summary);
        Assert.Contains("1 triggered of 2 configured", summary.TextContent);
    }

    [Fact]
    public async Task TriggeredAlert_DefaultViewShowsSummaryWithoutMatchingTransactions()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Groceries overspend");
        var outcome = CreateOutcome(
            alertId: alertId,
            title: "Groceries overspend",
            currentValue: 1250m,
            threshold: 1000m,
            comparisonOperator: AlertComparisonOperator.GreaterThan,
            matchingCount: 5);

        await using var context = CreateContext(outcomes: [outcome], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        var row = cut.Find("[data-testid='alert-summary-row']");
        Assert.Contains("Groceries overspend", row.TextContent);
        Assert.Contains(AlertPresentation.ComparisonDetail(outcome), row.TextContent);
        Assert.Contains("5 matches", row.TextContent);
        Assert.DoesNotContain("data-testid=\"alert-matching-transactions\"", cut.Markup);
    }

    [Fact]
    public async Task TriggeredAlerts_GroupRepeatedEvaluationsIntoOneSummaryRow()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Repeated alert");
        var older = CreateOutcome(
            alertId: alertId,
            title: "Repeated alert",
            matchingCount: 2,
            evaluatedAt: new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
        var newer = CreateOutcome(
            alertId: alertId,
            title: "Repeated alert",
            matchingCount: 4,
            evaluatedAt: new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc));

        await using var context = CreateContext(outcomes: [older, newer], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        var rows = cut.FindAll("[data-testid='alert-summary-row']");
        Assert.Single(rows);
        Assert.Contains("4 matches", rows[0].TextContent);
    }

    [Fact]
    public async Task TriggeredAlert_RemovesWarningIconAndLeftGutter()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Utility bill high");
        var outcome = CreateOutcome(alertId: alertId, title: "Utility bill high", message: "Exceeded usual amount");

        await using var context = CreateContext(outcomes: [outcome], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        // Ensure WarningAmber icon is completely removed from the alert items
        Assert.DoesNotContain("WarningAmber", cut.Markup);

        // Ensure list item content is not pushed by an icon column / gutter
        var itemContent = cut.Find(".mud-list-item div.w-100");
        Assert.NotNull(itemContent);
        Assert.Contains("d-flex flex-column", itemContent.ClassName);
    }

    [Fact]
    public async Task SelectingAlert_ShowsDetailFieldsAndInspectableTransaction()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Large transaction");
        var tx = new AlertTransactionReference(
            AccountId: 5,
            EntryId: 42,
            PostingDate: new DateTime(2026, 9, 10),
            Amount: 5000m,
            Description: "Tech Store",
            ContractorDetails: "Apple Inc");

        var outcome = CreateOutcome(
            alertId: alertId,
            title: "Large transaction",
            matchingTransactions: [tx],
            matchingCount: 3);
        var detailedOutcome = CreateOutcome(
            alertId: alertId,
            title: "Large transaction",
            matchingTransactions: [tx],
            matchingCount: 1);

        await using var context = CreateContext(
            outcomes: [outcome],
            alerts: [alert],
            detailedOutcomes: new Dictionary<Guid, AlertEvaluationOutcome> { [alertId] = detailedOutcome });
        var cut = context.Render<FinancialAlertsCard>();

        await cut.Find("[data-testid='alert-summary-row']").ClickAsync();

        var header = cut.Find("[data-testid='alert-card-header']");
        Assert.Contains("Large transaction", header.TextContent);
        Assert.NotNull(header.QuerySelector("button[aria-label='Back to alerts']"));
        Assert.Contains("1 match", header.TextContent);

        var detail = cut.Find("[data-testid='alert-detail-occurrences']");
        Assert.Contains("10 Sep 2026", detail.TextContent);
        Assert.Contains("Account", detail.TextContent);
        Assert.Contains("#5", detail.TextContent);
        Assert.Contains("Transaction #42", detail.TextContent);
        Assert.Contains(5_000m.ToString("N2"), detail.TextContent);

        var link = detail.QuerySelector("a[href='/AccountDetails/5?entryId=42']");
        Assert.NotNull(link);
        Assert.Equal("Apple Inc", link.TextContent.Trim());
        Assert.Equal("Inspect Apple Inc · 10 Sep 2026", link.GetAttribute("aria-label"));

        await cut.Find("button[aria-label='Back to alerts']").ClickAsync();
        Assert.NotNull(cut.Find("[data-testid='alert-summary-list']"));
        Assert.Empty(cut.FindAll("[data-testid='alert-detail-occurrences']"));
    }

    [Fact]
    public async Task SelectedAlert_DetailUsesCompactResponsiveOccurrenceMarkup()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Long title test");
        var tx = new AlertTransactionReference(
            AccountId: 1,
            EntryId: 2,
            PostingDate: new DateTime(2026, 9, 1),
            Amount: 100m,
            Description: "A Very Long Contractor Details And Description Exceeding Bounds",
            ContractorDetails: null);

        var outcome = CreateOutcome(
            alertId: alertId,
            title: "Long title test",
            matchingTransactions: [tx],
            matchingCount: 1);

        await using var context = CreateContext(
            outcomes: [outcome],
            alerts: [alert],
            detailedOutcomes: new Dictionary<Guid, AlertEvaluationOutcome> { [alertId] = outcome });
        var cut = context.Render<FinancialAlertsCard>();

        await cut.Find("[data-testid='alert-summary-row']").ClickAsync();

        var container = cut.Find("[data-testid='alert-detail-occurrences']");
        Assert.NotNull(container);
        Assert.Contains("pa-2", container.ClassName);

        var link = cut.Find("a[href='/AccountDetails/1?entryId=2']");
        Assert.Contains("d-block", link.ClassName);
        var linkStyle = link.GetAttribute("style")?.Replace(" ", "") ?? "";
        Assert.Contains("min-width:0", linkStyle);
        Assert.Contains("white-space:normal", linkStyle);
        Assert.Contains("overflow-wrap:anywhere", linkStyle);
        Assert.Contains("Transaction #2", container.TextContent);
    }

    [Fact]
    public async Task AllConfiguredAlertsHealthy_DisplaysHealthyMessage()
    {
        var alert = CreateAlertDto(title: "Active rule");

        await using var context = CreateContext(outcomes: [], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        Assert.Contains("All configured alerts are healthy.", cut.Markup);
    }

    [Fact]
    public async Task EvaluationError_DisplaysErrorMessage()
    {
        await using var context = CreateContext(throwOnEvaluate: true);
        var cut = context.Render<FinancialAlertsCard>();

        Assert.Contains("Unable to load alerts.", cut.Markup);
    }

    [Fact]
    public async Task SelectedAlert_DetailFallsBackToDescriptionWhenContractorDetailsEmpty()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Card debit");
        var tx = new AlertTransactionReference(
            AccountId: 3,
            EntryId: 17,
            PostingDate: new DateTime(2026, 9, 8),
            Amount: 200m,
            Description: "Online Service Monthly",
            ContractorDetails: null);

        var outcome = CreateOutcome(
            alertId: alertId,
            title: "Card debit",
            matchingTransactions: [tx],
            matchingCount: 1);

        await using var context = CreateContext(
            outcomes: [outcome],
            alerts: [alert],
            detailedOutcomes: new Dictionary<Guid, AlertEvaluationOutcome> { [alertId] = outcome });
        var cut = context.Render<FinancialAlertsCard>();

        await cut.Find("[data-testid='alert-summary-row']").ClickAsync();

        var link = cut.Find("[data-testid='alert-detail-occurrences'] a[href='/AccountDetails/3?entryId=17']");
        Assert.Equal("Online Service Monthly", link.TextContent.Trim());
        Assert.Contains("8 Sep 2026", cut.Markup);
    }

    [Fact]
    public async Task SelectedAlert_DetailFallsBackToTransactionWhenBothContractorAndDescriptionEmpty()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Unknown fee");
        var tx = new AlertTransactionReference(
            AccountId: 4,
            EntryId: 88,
            PostingDate: new DateTime(2026, 9, 5),
            Amount: 15m,
            Description: "   ",
            ContractorDetails: null);

        var outcome = CreateOutcome(
            alertId: alertId,
            title: "Unknown fee",
            matchingTransactions: [tx],
            matchingCount: 1);

        await using var context = CreateContext(
            outcomes: [outcome],
            alerts: [alert],
            detailedOutcomes: new Dictionary<Guid, AlertEvaluationOutcome> { [alertId] = outcome });
        var cut = context.Render<FinancialAlertsCard>();

        await cut.Find("[data-testid='alert-summary-row']").ClickAsync();

        var link = cut.Find("[data-testid='alert-detail-occurrences'] a[href='/AccountDetails/4?entryId=88']");
        Assert.Equal("Transaction", link.TextContent.Trim());
        Assert.Contains("5 Sep 2026", cut.Markup);
    }

    [Fact]
    public async Task MatchingTransactions_RendersNothingWhenEmpty()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Balance check");
        var outcome = CreateOutcome(
            alertId: alertId,
            title: "Balance check",
            matchingTransactions: [],
            matchingCount: 0);

        await using var context = CreateContext(outcomes: [outcome], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        Assert.DoesNotContain("/AccountDetails/", cut.Markup);
    }

    private BunitContext CreateContext(
        List<AlertEvaluationOutcome>? outcomes = null,
        List<FinancialAlertDto>? alerts = null,
        bool throwOnEvaluate = false,
        IReadOnlyDictionary<Guid, AlertEvaluationOutcome>? detailedOutcomes = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var handler = new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/evaluate", StringComparison.OrdinalIgnoreCase))
            {
                if (throwOnEvaluate)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                AlertEvaluationOutcome? detailedOutcome = null;
                if (segments.Length >= 4
                    && Guid.TryParse(segments[^2], out var detailedAlertId))
                {
                    if (detailedOutcomes is not null
                        && detailedOutcomes.TryGetValue(detailedAlertId, out var outcome))
                    {
                        detailedOutcome = outcome;
                    }
                }

                if (segments.Length >= 4)
                {
                    if (detailedOutcome is null)
                        return new HttpResponseMessage(HttpStatusCode.NotFound);

                    var detailJson = JsonSerializer.Serialize(detailedOutcome);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(detailJson, Encoding.UTF8, "application/json")
                    };
                }

                var json = JsonSerializer.Serialize(outcomes ?? []);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            if (path.EndsWith("/FinancialAlerts", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(alerts ?? []);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        AddCardServices(context, handler);
        return context;
    }

    private void AddCardServices(BunitContext context, HttpMessageHandler handler)
    {
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(
            new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new FinancialAlertsHttpClient(httpClient));
    }

    [Fact]
    public async Task HydratesBeforeRequestCompletes_AndFetchesOnEveryVisit()
    {
        Stored();
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var first = context.Render<FinancialAlertsCard>();
        first.WaitForAssertion(() => Assert.Contains("1 triggered of 2 configured", Subtitle(first)), _timeout);
        Assert.Contains("Stored alert", first.Find("[data-testid='alert-summary-row']").TextContent);
        first.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        handler.Complete(0, [StoredOutcome()], StoredAlerts());
        await Drain(first);
        first.Dispose();

        var second = context.Render<FinancialAlertsCard>();
        second.WaitForAssertion(() => Assert.Equal(2, handler.EvaluationCount), _timeout);
        Assert.Contains("Stored alert", second.Find("[data-testid='alert-summary-row']").TextContent);
        handler.Complete(1, [StoredOutcome()], StoredAlerts());
        await Drain(second);
    }

    [Fact]
    public async Task EqualSummary_DoesNotWrite_EvenWhenNonRenderedFieldsDiffer()
    {
        Stored();
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        var outcome = StoredOutcome() with
        {
            EvaluatedAt = DateTime.UtcNow.AddDays(3),
            Message = "different message",
            ConditionFingerprint = "other",
            Context = new Dictionary<string, string> { ["k"] = "v" },
        };
        handler.Complete(0, [outcome], StoredAlerts());
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FinancialAlertsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ChangedSummary_RepaintsAndWritesWithKey()
    {
        Stored();
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        handler.Complete(0, [StoredOutcome() with { CurrentValue = 300m }], StoredAlerts());
        cut.WaitForAssertion(() => Assert.Contains(300m.ToString("N2"), cut.Find("[data-testid='alert-summary-row']").TextContent), _timeout);
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<FinancialAlertsSnapshot>(s =>
            s.UserId == 1 && s.Summary.ConfiguredCount == 2 && s.Summary.Triggered.Count == 1
            && s.Summary.Triggered[0].CurrentValue == 300m && !s.Summary.HasEvaluationError)), Times.Once);
    }

    [Fact]
    public async Task EmptySuccess_ClearsAndWrites()
    {
        Stored();
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        handler.Complete(0, [], []);
        cut.WaitForAssertion(() => Assert.Contains("All configured alerts are healthy.", cut.Markup), _timeout);
        await Drain(cut);
        Assert.DoesNotContain("Unable to load alerts.", cut.Markup);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<FinancialAlertsSnapshot>(s =>
            s.Summary.ConfiguredCount == 0 && s.Summary.Triggered.Count == 0 && !s.Summary.HasEvaluationError)), Times.Once);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task EvaluateOrListFailure_PreservesSnapshotOrShowsBlockingError(bool stored, bool evaluateFails)
    {
        if (stored) Stored();
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        if (evaluateFails) handler.FailEvaluation(0); else handler.CompleteEvaluation(0, [StoredOutcome()]);
        if (evaluateFails) handler.CompleteList(0, StoredAlerts()); else handler.FailList(0);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(!stored, cut.Markup.Contains("Unable to load alerts."));
            Assert.Equal(stored, cut.FindAll("[data-testid='alert-summary-row']").Count == 1);
        }, _timeout);
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FinancialAlertsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task EvaluationErrorOutcome_ShowsErrorAndIsStored()
    {
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        handler.Complete(0, [CreateOutcome(title: "Broken", isTriggered: false, status: AlertTriggerStatus.Error)], StoredAlerts());
        cut.WaitForAssertion(() => Assert.Contains("Unable to load alerts.", cut.Markup), _timeout);
        await Drain(cut);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<FinancialAlertsSnapshot>(s => s.Summary.HasEvaluationError)), Times.Once);
    }

    [Fact]
    public async Task SnapshotOfAnotherUser_IsNotPainted()
    {
        Stored(userId: 2);
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        Assert.Empty(cut.FindAll("[data-testid='alert-summary-row']"));
        Assert.DoesNotContain("Stored alert", cut.Markup);
        Assert.DoesNotContain("triggered of", cut.Markup);
        handler.Complete(0, [], []);
        cut.WaitForAssertion(() => Assert.Contains("All configured alerts are healthy.", cut.Markup), _timeout);
        await Drain(cut);
    }

    [Fact]
    public async Task DisposedRun_DoesNotCommit()
    {
        Stored();
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        await context.DisposeComponentsAsync();
        handler.Complete(0, [StoredOutcome() with { CurrentValue = 999m }], StoredAlerts());
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FinancialAlertsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task StorageReadFailure_StillFetchesAndRenders()
    {
        _snapshots.Setup(x => x.GetAsync<FinancialAlertsSnapshot>(_key)).ThrowsAsync(new InvalidOperationException());
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        handler.Complete(0, [StoredOutcome()], StoredAlerts());
        cut.WaitForAssertion(() => Assert.Contains("Stored alert", cut.Markup), _timeout);
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
        await Drain(cut);
    }

    [Fact]
    public async Task StorageWriteFailure_KeepsFreshSummary()
    {
        _snapshots.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FinancialAlertsSnapshot>())).ThrowsAsync(new InvalidOperationException());
        var handler = new AlertsHandler();
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        handler.Complete(0, [StoredOutcome()], StoredAlerts());
        cut.WaitForAssertion(() => Assert.Contains("Stored alert", cut.Markup), _timeout);
        await Drain(cut);
        Assert.DoesNotContain("Unable to load alerts.", cut.Markup);
    }

    [Fact]
    public async Task ClickOpenDetail_WorksOnPaintedSnapshot_AndIsNeverSnapshotted()
    {
        Stored();
        var tx = new AlertTransactionReference(5, 42, new DateTime(2026, 9, 10), 5000m, "Tech Store", "Apple Inc");
        var detailed = StoredOutcome() with { MatchingTransactions = [tx], OccurrenceCount = 1 };
        var handler = new AlertsHandler(new Dictionary<Guid, AlertEvaluationOutcome> { [_storedAlertId] = detailed });
        await using var context = NewContext(handler);
        var cut = context.Render<FinancialAlertsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.EvaluationCount), _timeout);
        await cut.Find("[data-testid='alert-summary-row']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Contains("Transaction #42", cut.Find("[data-testid='alert-detail-occurrences']").TextContent), _timeout);
        Assert.Contains("Stored alert", cut.Find("[data-testid='alert-card-header']").TextContent);

        handler.Complete(0, [StoredOutcome()], StoredAlerts());
        await Drain(cut);
        Assert.NotNull(cut.Find("[data-testid='alert-detail-occurrences']"));
        await cut.Find("button[aria-label='Back to alerts']").ClickAsync();
        Assert.NotNull(cut.Find("[data-testid='alert-summary-list']"));
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FinancialAlertsSnapshot>()), Times.Never);
    }

    private BunitContext NewContext(HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        AddCardServices(context, handler);
        return context;
    }

    private void Stored(int userId = 1) =>
        _snapshots.Setup(x => x.GetAsync<FinancialAlertsSnapshot>(_key)).ReturnsAsync(new FinancialAlertsSnapshot
        {
            UserId = userId,
            FetchedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Summary = new FinancialAlertsSummaryModel(
                2,
                [new FinancialAlertSummaryItem(_storedAlertId, "Stored alert", 100m, 50m, AlertComparisonOperator.GreaterThan, 2)],
                false),
        });

    private static AlertEvaluationOutcome StoredOutcome() =>
        CreateOutcome(alertId: _storedAlertId, title: "Stored alert", currentValue: 100m, threshold: 50m, matchingCount: 2);

    private static List<FinancialAlertDto> StoredAlerts() => [CreateAlertDto(title: "One"), CreateAlertDto(title: "Two")];

    private static string Subtitle(IRenderedComponent<FinancialAlertsCard> cut) =>
        cut.Find(".mud-typography-subtitle1.text-muted").TextContent;

    private static Task Drain(IRenderedComponent<FinancialAlertsCard> cut) => cut.InvokeAsync(async () => await Task.Delay(50));

    private sealed class AlertsHandler(IReadOnlyDictionary<Guid, AlertEvaluationOutcome>? details = null) : HttpMessageHandler
    {
        private readonly Lock _lock = new();
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _evaluations = [];
        private readonly List<TaskCompletionSource<HttpResponseMessage>> _lists = [];

        public int EvaluationCount { get { lock (_lock) return _evaluations.Count; } }

        public void Complete(int index, List<AlertEvaluationOutcome> outcomes, List<FinancialAlertDto> alerts)
        {
            CompleteEvaluation(index, outcomes);
            CompleteList(index, alerts);
        }

        public void CompleteEvaluation(int index, List<AlertEvaluationOutcome> outcomes) => Pending(_evaluations, index).SetResult(Json(outcomes));

        public void CompleteList(int index, List<FinancialAlertDto> alerts) => Pending(_lists, index).SetResult(Json(alerts));

        public void FailEvaluation(int index) => Pending(_evaluations, index).SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        public void FailList(int index) => Pending(_lists, index).SetResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (path.EndsWith("/evaluate", StringComparison.OrdinalIgnoreCase) && segments.Length >= 4)
            {
                if (details is not null && Guid.TryParse(segments[^2], out var id) && details.TryGetValue(id, out var detail))
                    return Task.FromResult(Json(detail));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
                (path.EndsWith("/evaluate", StringComparison.OrdinalIgnoreCase) ? _evaluations : _lists).Add(pending);
            return pending.Task;
        }

        private TaskCompletionSource<HttpResponseMessage> Pending(List<TaskCompletionSource<HttpResponseMessage>> list, int index)
        {
            lock (_lock) return list[index];
        }

        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json"),
        };
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private static AlertEvaluationOutcome CreateOutcome(
        Guid? alertId = null,
        string title = "Test Alert",
        bool isTriggered = true,
        string message = "Test Message",
        decimal currentValue = 100m,
        decimal threshold = 50m,
        AlertComparisonOperator comparisonOperator = AlertComparisonOperator.GreaterThan,
        AlertTriggerStatus status = AlertTriggerStatus.Triggered,
        IReadOnlyList<AlertTransactionReference>? matchingTransactions = null,
        int matchingCount = 0,
        DateTime? evaluatedAt = null)
    {
        var evaluationTime = evaluatedAt ?? DateTime.UtcNow;
        return new AlertEvaluationOutcome(
            AlertId: alertId ?? Guid.NewGuid(),
            AlertTitle: title,
            AlertType: AlertType.LargeTransaction,
            Status: status,
            IsTriggered: isTriggered,
            TriggeredAt: evaluationTime,
            IsSuppressed: false,
            DeDuplicationReason: DeDuplicationReason.None,
            CurrentValue: currentValue,
            Threshold: threshold,
            ComparisonOperator: comparisonOperator,
            ConditionFingerprint: "fingerprint",
            Message: message,
            EvaluatedAt: evaluationTime,
            Context: new Dictionary<string, string>(),
            ErrorMessage: null,
            MatchingTransactions: matchingTransactions,
            MatchingTransactionCount: matchingCount);
    }

    private static FinancialAlertDto CreateAlertDto(Guid? id = null, string title = "Test Alert")
    {
        return new FinancialAlertDto(
            Id: id ?? Guid.NewGuid(),
            UserId: 1,
            Title: title,
            AlertType: AlertType.LargeTransaction,
            IsEnabled: true,
            ComparisonOperator: AlertComparisonOperator.GreaterThan,
            Threshold: 50m,
            EvaluationPeriod: AlertEvaluationPeriod.CurrentMonth,
            AccountId: null,
            LabelId: null,
            LabelName: null,
            MerchantName: null,
            SubscriptionId: null,
            LastStatus: AlertTriggerStatus.Healthy,
            LastTriggeredAt: null,
            LastTriggeredValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: null);
    }
}