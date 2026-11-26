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

    /// <summary>Observed value and threshold, each with the alert's currency code so alerts in different currencies read apart.</summary>
    public static string ComparisonDetail(AlertEvaluationOutcome outcome)
    {
        var code = outcome.Context is not null && outcome.Context.TryGetValue("CurrencyCode", out var value) && !string.IsNullOrWhiteSpace(value)
            ? $" {value}"
            : string.Empty;
        return $"{MoneyFormatter.FormatNumber(outcome.CurrentValue)}{code} {ComparisonSymbol(outcome.ComparisonOperator)} {MoneyFormatter.FormatNumber(outcome.Threshold)}{code}";
    }

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

    // Presets of the transaction log's range menu, from the shortest to the longest window they can open on.
    private static readonly string[] _logRangePresets = ["Month", "1M", "3M", "6M", "YTD"];

    /// <summary>
    /// Opens the transaction log filtered to the transaction's text and currency; its description is preferred so counterparty
    /// names stay out of URLs. The range always contains the posting date: the narrowest preset that reaches it, or an explicit
    /// start day when the transaction is older than every preset (the log opens on its three-month default otherwise).
    /// </summary>
    public static string TransactionHref(AlertTransactionReference transaction, DateTime? now = null)
    {
        var term = !string.IsNullOrWhiteSpace(transaction.Description) ? transaction.Description : transaction.ContractorDetails;
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(term))
            parameters.Add($"search={Uri.EscapeDataString(term.Trim())}");
        if (!string.IsNullOrWhiteSpace(transaction.CurrencyCode))
            parameters.Add($"currency={Uri.EscapeDataString(transaction.CurrencyCode)}");
        parameters.Add(RangeParameter(transaction.PostingDate, now ?? DateTime.UtcNow));

        return $"/Transactions?{string.Join('&', parameters)}";
    }

    private static string RangeParameter(DateTime postingDate, DateTime now)
    {
        string? best = null;
        var bestStart = DateTime.MinValue;
        foreach (var preset in _logRangePresets)
        {
            var start = DateRangeHelper.GetAccountDetailsRange(preset, null, null, now, now, now).Start.Date;
            if (start > postingDate.Date || start <= bestStart) continue;
            best = preset;
            bestStart = start;
        }

        return best is null
            ? $"from={postingDate.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}"
            : $"range={best}";
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

    /// <summary>
    /// A cash alert that cannot run until the user picks a currency: none is stored (migrated from an account scope), or the
    /// stored one is not in the currency catalog. <paramref name="knownCurrencyIds"/> is null while the catalog is not loaded.
    /// </summary>
    public static bool NeedsCurrency(FinancialAlertDto alert, IReadOnlySet<int>? knownCurrencyIds = null) =>
        alert.AlertType.RequiresCurrency()
        && (alert.CurrencyId is null || (alert.CurrencyId is int id && knownCurrencyIds is not null && !knownCurrencyIds.Contains(id)));

    public static string ConditionLabel(FinancialAlertDto alert, string? currencyCode = null, IReadOnlySet<int>? knownCurrencyIds = null)
    {
        var condition = $"{ComparisonLabel(alert.ComparisonOperator)} {MoneyFormatter.FormatNumber(alert.Threshold)}";
        if (NeedsCurrency(alert, knownCurrencyIds))
            return $"{condition} (currency required)";

        return string.IsNullOrWhiteSpace(currencyCode) ? condition : $"{condition} {currencyCode}";
    }

    /// <summary>
    /// Status from the latest evaluation when there is one, otherwise from the alert's stored last status. A disabled alert
    /// is "Needs currency" when it waits for the user's currency choice, otherwise plain "Disabled".
    /// </summary>
    public static string StatusLabel(FinancialAlertDto alert, AlertEvaluationOutcome? outcome, IReadOnlySet<int>? knownCurrencyIds = null)
    {
        if (!alert.IsEnabled)
            return NeedsCurrency(alert, knownCurrencyIds) ? "Needs currency" : "Disabled";

        return outcome is not null
            ? outcome.Status switch
            {
                AlertTriggerStatus.Triggered => "Triggered",
                AlertTriggerStatus.Error => "Error",
                _ => "Healthy"
            }
            : alert.LastStatus == AlertTriggerStatus.Triggered ? "Triggered" : "Healthy";
    }

    public static Color StatusColor(FinancialAlertDto alert, AlertEvaluationOutcome? outcome, IReadOnlySet<int>? knownCurrencyIds = null) =>
        StatusLabel(alert, outcome, knownCurrencyIds) switch
        {
            "Triggered" or "Needs currency" => Color.Warning,
            "Error" => Color.Error,
            "Disabled" => Color.Default,
            _ => Color.Success
        };
}