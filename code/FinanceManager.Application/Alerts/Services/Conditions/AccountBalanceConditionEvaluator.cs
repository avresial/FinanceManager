using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using System.Globalization;

namespace FinanceManager.Application.Alerts.Services.Conditions;

/// <summary>Compares the pooled cash balance of one currency with the threshold.</summary>
public sealed class AccountBalanceConditionEvaluator : IAlertConditionEvaluator
{
    public AlertType AlertType => AlertType.AccountBalance;

    public AlertConditionResult Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var currencyId = AlertCurrency.Require(alert);
        var inv = CultureInfo.InvariantCulture;
        var currencyCode = AlertCurrency.CodeOf(snapshot, currencyId);
        var balance = snapshot.CashPositions.FirstOrDefault(position => position.PartitionId == currencyId)?.Value ?? 0m;

        var context = new Dictionary<string, string>
        {
            ["CurrencyId"] = currencyId.ToString(inv),
            ["CurrencyCode"] = currencyCode,
            ["Balance"] = balance.ToString("F2", inv)
        };

        var conditionMet = AlertComparison.Matches(balance, alert.ComparisonOperator, alert.Threshold);
        var fingerprint = $"AccountBalance:CurrencyId={currencyId}:Balance={balance.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var message = $"Cash balance ({currencyCode}) is {balance.ToString("N2", inv)}";

        return new AlertConditionResult(conditionMet, balance, fingerprint, message, context, OccurrenceCount: conditionMet ? 1 : 0);
    }
}