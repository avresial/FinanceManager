using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Application.Alerts.Services.Conditions;

internal static class AlertComparison
{
    public static bool Matches(decimal actual, AlertComparisonOperator op, decimal threshold) => op switch
    {
        AlertComparisonOperator.GreaterThan => actual > threshold,
        AlertComparisonOperator.GreaterThanOrEqual => actual >= threshold,
        AlertComparisonOperator.LessThan => actual < threshold,
        AlertComparisonOperator.LessThanOrEqual => actual <= threshold,
        AlertComparisonOperator.Equal => actual == threshold,
        AlertComparisonOperator.NotEqual => actual != threshold,
        _ => false
    };
}