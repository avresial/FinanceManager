using Bunit;
using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.Components;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public sealed class FinancialAlertsCardTests
{
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
    public async Task TriggeredAlert_PreservesHierarchy_TitleMessageComparisonMatchingTransactions()
    {
        var alertId = Guid.NewGuid();
        var alert = CreateAlertDto(id: alertId, title: "Groceries overspend");
        var tx = new AlertTransactionReference(
            AccountId: 10,
            EntryId: 99,
            PostingDate: new DateTime(2026, 9, 14),
            Amount: 1250m,
            Description: "Supermarket Purchase",
            ContractorDetails: "Supermarket");

        var outcome = CreateOutcome(
            alertId: alertId,
            title: "Groceries overspend",
            message: "Monthly budget exceeded",
            currentValue: 1250m,
            threshold: 1000m,
            comparisonOperator: AlertComparisonOperator.GreaterThan,
            matchingTransactions: [tx],
            matchingCount: 1);

        await using var context = CreateContext(outcomes: [outcome], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        var itemContent = cut.Find(".mud-list-item div.w-100");
        Assert.NotNull(itemContent);

        // Verify hierarchy: Title -> Message -> Comparison -> Matching transactions
        var elements = itemContent.Children;
        Assert.True(elements.Length >= 4, "Expected at least 4 child elements for hierarchy");

        // The DOM element order guarantees the visual and DOM hierarchy
        Assert.Equal("Groceries overspend", elements[0].TextContent.Trim());
        Assert.Equal("Monthly budget exceeded", elements[1].TextContent.Trim());
        Assert.Equal(AlertPresentation.ComparisonDetail(outcome), elements[2].TextContent.Trim());
        Assert.Contains("Supermarket", elements[3].TextContent);
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
    public async Task MatchingTransactions_RendersMetadataInMutedStylingRatherThanAccentOrange()
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

        await using var context = CreateContext(outcomes: [outcome], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        // Link text is the transaction title
        var link = cut.Find("a[href='/AccountDetails/5?entryId=42']");
        Assert.NotNull(link);
        Assert.Equal("Apple Inc", link.TextContent.Trim());
        Assert.Equal("Inspect Apple Inc · 2026-09-10", link.GetAttribute("aria-label"));

        // Transaction date metadata is outside the link and styled muted
        var metadataElement = cut.Find("[data-testid='alert-matching-transactions'] .mud-text-secondary");
        Assert.NotNull(metadataElement);
        Assert.Contains("· 2026-09-10", metadataElement.TextContent);

        // The "+N more matching" text must use mud-text-secondary styling rather than accent orange
        var moreMatching = cut.FindAll(".mud-text-secondary")
            .FirstOrDefault(e => e.TextContent.Contains("+2 more matching"));
        Assert.NotNull(moreMatching);
    }

    [Fact]
    public async Task MatchingTransactions_AppliesCompactPresentationAndTruncationClasses()
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

        await using var context = CreateContext(outcomes: [outcome], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        // Every matching transaction occupies its own compact row and the container stays within the card.
        var container = cut.Find("[data-testid='alert-matching-transactions']");
        Assert.NotNull(container);
        Assert.Contains("d-flex flex-column", container.ClassName);
        Assert.DoesNotContain("flex-wrap", container.ClassName);
        Assert.Contains("max-width:100%", container.GetAttribute("style")?.Replace(" ", "") ?? "");

        // Verify the link can shrink and truncate long transaction titles.
        var link = cut.Find("a[href='/AccountDetails/1?entryId=2']");
        Assert.Contains("text-truncate", link.ClassName);
        Assert.Contains("flex-grow-1", link.ClassName);
        Assert.Contains("min-width:0", link.GetAttribute("style")?.Replace(" ", "") ?? "");
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
    public async Task MatchingTransactions_FallsBackToDescriptionWhenContractorDetailsEmpty()
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

        await using var context = CreateContext(outcomes: [outcome], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        var link = cut.Find("a[href='/AccountDetails/3?entryId=17']");
        Assert.Equal("Online Service Monthly", link.TextContent.Trim());
        Assert.Contains("· 2026-09-08", cut.Markup);
    }

    [Fact]
    public async Task MatchingTransactions_FallsBackToTransactionWhenBothContractorAndDescriptionEmpty()
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

        await using var context = CreateContext(outcomes: [outcome], alerts: [alert]);
        var cut = context.Render<FinancialAlertsCard>();

        var link = cut.Find("a[href='/AccountDetails/4?entryId=88']");
        Assert.Equal("Transaction", link.TextContent.Trim());
        Assert.Contains("on 2026-09-05", cut.Markup);
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

    private static BunitContext CreateContext(
        List<AlertEvaluationOutcome>? outcomes = null,
        List<FinancialAlertDto>? alerts = null,
        bool throwOnEvaluate = false)
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

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new FinancialAlertsHttpClient(httpClient));

        return context;
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
        int matchingCount = 0)
    {
        return new AlertEvaluationOutcome(
            AlertId: alertId ?? Guid.NewGuid(),
            AlertTitle: title,
            AlertType: AlertType.LargeTransaction,
            Status: status,
            IsTriggered: isTriggered,
            TriggeredAt: DateTime.UtcNow,
            IsSuppressed: false,
            DeDuplicationReason: DeDuplicationReason.None,
            CurrentValue: currentValue,
            Threshold: threshold,
            ComparisonOperator: comparisonOperator,
            ConditionFingerprint: "fingerprint",
            Message: message,
            EvaluatedAt: DateTime.UtcNow,
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