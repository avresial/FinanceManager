using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using MudBlazor;

namespace FinanceManager.Components.Features.Alerts.Components;

public static class AlertPresentation
{
    public static int OccurrenceCount(AlertEvaluationOutcome outcome) =>
        outcome.OccurrenceCount > 0
            ? outcome.OccurrenceCount
            : outcome.MatchingTransactionCount > 0
                ? outcome.MatchingTransactionCount
                : outcome.MatchingTransactions?.Count ?? 0;

    public static string OccurrenceLabel(AlertEvaluationOutcome outcome)
    {
        var count = OccurrenceCount(outcome);
        return $"{count} {(count == 1 ? "match" : "matches")}";
    }

    public static string ComparisonDetail(AlertEvaluationOutcome outcome) =>
        $"{MoneyFormatter.FormatNumber(outcome.CurrentValue)} {ComparisonSymbol(outcome.ComparisonOperator)} {MoneyFormatter.FormatNumber(outcome.Threshold)}";

    public static string ComparisonSymbol(AlertComparisonOperator comparison) => comparison switch
    {
        AlertComparisonOperator.GreaterThan => ">",
        AlertComparisonOperator.GreaterThanOrEqual => ">=",
        AlertComparisonOperator.LessThan => "<",
        AlertComparisonOperator.LessThanOrEqual => "<=",
        AlertComparisonOperator.Equal => "=",
        AlertComparisonOperator.NotEqual => "!=",
        _ => comparison.ToString()
    };

    /// <summary>Opens the transaction log filtered to the transaction's text and currency; its description is preferred so counterparty names stay out of URLs.</summary>
    public static string TransactionHref(AlertTransactionReference transaction)
    {
        var term = !string.IsNullOrWhiteSpace(transaction.Description) ? transaction.Description : transaction.ContractorDetails;
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(term))
            parameters.Add($"search={Uri.EscapeDataString(term.Trim())}");
        if (!string.IsNullOrWhiteSpace(transaction.CurrencyCode))
            parameters.Add($"currency={Uri.EscapeDataString(transaction.CurrencyCode)}");

        return parameters.Count == 0 ? "/Transactions" : $"/Transactions?{string.Join('&', parameters)}";
    }

    public static string TransactionTitle(AlertTransactionReference transaction) =>
        !string.IsNullOrWhiteSpace(transaction.ContractorDetails)
            ? transaction.ContractorDetails
            : string.IsNullOrWhiteSpace(transaction.Description)
                ? "Transaction"
                : transaction.Description;

    public static string TransactionLabel(AlertTransactionReference transaction)
    {
        var title = TransactionTitle(transaction);
        return string.Equals(title, "Transaction", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(transaction.ContractorDetails)
            && string.IsNullOrWhiteSpace(transaction.Description)
            ? $"Transaction on {DateFormatter.Format(transaction.PostingDate)}"
            : $"{title} · {DateFormatter.Format(transaction.PostingDate)}";
    }

    public static string TransactionAriaLabel(AlertTransactionReference transaction) =>
        $"Inspect {TransactionLabel(transaction)}";

    public static string TypeLabel(AlertType type) => type switch
    {
        AlertType.AccountBalance => "Cash balance",
        AlertType.CategorySpending => "Category spending",
        AlertType.MerchantSpending => "Merchant spending",
        AlertType.LargeTransaction => "Large transaction",
        AlertType.SubscriptionPriceChange => "Subscription price change",
        _ => type.ToString()
    };

    /// <summary>Typographic operator for the form and alert list; <see cref="ComparisonSymbol"/> is the ASCII form used in evaluation details.</summary>
    public static string ComparisonLabel(AlertComparisonOperator comparison) => comparison switch
    {
        AlertComparisonOperator.GreaterThan => ">",
        AlertComparisonOperator.GreaterThanOrEqual => "≥",
        AlertComparisonOperator.LessThan => "<",
        AlertComparisonOperator.LessThanOrEqual => "≤",
        AlertComparisonOperator.Equal => "=",
        AlertComparisonOperator.NotEqual => "≠",
        _ => comparison.ToString()
    };

    public static string PeriodLabel(AlertEvaluationPeriod period) => period switch
    {
        AlertEvaluationPeriod.CurrentMonth => "Current month",
        AlertEvaluationPeriod.Last30Days => "Last 30 days",
        AlertEvaluationPeriod.Last7Days => "Last 7 days",
        AlertEvaluationPeriod.AllTime => "All time",
        _ => period.ToString()
    };

    public static bool NeedsCurrency(FinancialAlertDto alert) =>
        alert.AlertType.RequiresCurrency() && alert.CurrencyId is null;

    public static string ConditionLabel(FinancialAlertDto alert, string? currencyCode = null)
    {
        var condition = $"{ComparisonLabel(alert.ComparisonOperator)} {MoneyFormatter.FormatNumber(alert.Threshold)}";
        if (NeedsCurrency(alert))
            return $"{condition} (currency required)";

        return string.IsNullOrWhiteSpace(currencyCode) ? condition : $"{condition} {currencyCode}";
    }

    /// <summary>Status from the latest evaluation when there is one, otherwise from the alert's stored last status.</summary>
    public static string StatusLabel(FinancialAlertDto alert, AlertEvaluationOutcome? outcome) =>
        outcome is not null
            ? outcome.Status switch
            {
                AlertTriggerStatus.Triggered => "Triggered",
                AlertTriggerStatus.Error => "Error",
                _ => "Healthy"
            }
            : alert.LastStatus == AlertTriggerStatus.Triggered ? "Triggered" : "Healthy";

    public static Color StatusColor(FinancialAlertDto alert, AlertEvaluationOutcome? outcome) =>
        StatusLabel(alert, outcome) switch
        {
            "Triggered" => Color.Warning,
            "Error" => Color.Error,
            _ => Color.Success
        };
}