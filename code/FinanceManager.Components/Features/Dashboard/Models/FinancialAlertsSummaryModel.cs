namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>What the Financial Alerts card summary renders: configured count, triggered rows in display order, and whether any evaluation errored.</summary>
public sealed record FinancialAlertsSummaryModel(
    int ConfiguredCount,
    IReadOnlyList<FinancialAlertSummaryItem> Triggered,
    bool HasEvaluationError);