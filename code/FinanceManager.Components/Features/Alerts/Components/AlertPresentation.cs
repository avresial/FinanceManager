using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Enums;

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
        $"{outcome.CurrentValue:N2} {ComparisonSymbol(outcome.ComparisonOperator)} {outcome.Threshold:N2}";

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

    public static string TransactionHref(AlertTransactionReference transaction) =>
        $"/AccountDetails/{transaction.AccountId}?entryId={transaction.EntryId}";

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
            ? $"Transaction on {transaction.PostingDate:yyyy-MM-dd}"
            : $"{title} · {transaction.PostingDate:yyyy-MM-dd}";
    }

    public static string TransactionAriaLabel(AlertTransactionReference transaction) =>
        $"Inspect {TransactionLabel(transaction)}";
}