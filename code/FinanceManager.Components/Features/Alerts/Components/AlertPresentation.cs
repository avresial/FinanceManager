using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Components.Features.Alerts.Components;

public static class AlertPresentation
{
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

    public static string TransactionLabel(AlertTransactionReference transaction)
    {
        var title = !string.IsNullOrWhiteSpace(transaction.ContractorDetails)
            ? transaction.ContractorDetails
            : transaction.Description;
        return string.IsNullOrWhiteSpace(title)
            ? $"Transaction on {transaction.PostingDate:yyyy-MM-dd}"
            : $"{title} · {transaction.PostingDate:yyyy-MM-dd}";
    }

    public static string TransactionAriaLabel(AlertTransactionReference transaction) =>
        $"Inspect {TransactionLabel(transaction)}";
}