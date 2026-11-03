using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using System.Globalization;

namespace FinanceManager.Application.Alerts.Services.Conditions;

public sealed class AccountBalanceConditionEvaluator : IAlertConditionEvaluator
{
    public AlertType AlertType => AlertType.AccountBalance;

    public AlertConditionResult Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var context = new Dictionary<string, string>();
        var inv = CultureInfo.InvariantCulture;

        if (alert.AccountId is int targetAccountId)
        {
            var account = snapshot.Accounts.FirstOrDefault(a => a.AccountId == targetAccountId);
            var balance = GetAccountBalance(account);
            var accountName = account?.Name ?? $"Account #{targetAccountId}";

            context["AccountId"] = targetAccountId.ToString(inv);
            context["AccountName"] = accountName;
            context["Balance"] = balance.ToString("F2", inv);

            var conditionMet = AlertComparison.Matches(balance, alert.ComparisonOperator, alert.Threshold);
            var fingerprint = $"AccountBalance:AccountId={targetAccountId}:Balance={balance.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
            var message = $"Account '{accountName}' balance is {balance.ToString("N2", inv)}";

            return new AlertConditionResult(conditionMet, balance, fingerprint, message, context, OccurrenceCount: conditionMet ? 1 : 0);
        }

        // AccountId is null: evaluate each available account
        CurrencyAccount? triggeredAccount = null;
        decimal? triggeredBalance = null;

        foreach (var account in snapshot.Accounts)
        {
            var balance = GetAccountBalance(account);
            if (AlertComparison.Matches(balance, alert.ComparisonOperator, alert.Threshold))
            {
                if (triggeredBalance is null)
                {
                    triggeredAccount = account;
                    triggeredBalance = balance;
                }
                else
                {
                    var isMoreExtreme = alert.ComparisonOperator is AlertComparisonOperator.LessThan or AlertComparisonOperator.LessThanOrEqual
                        ? balance < triggeredBalance.Value
                        : balance > triggeredBalance.Value;

                    if (isMoreExtreme)
                    {
                        triggeredAccount = account;
                        triggeredBalance = balance;
                    }
                }
            }
        }

        if (triggeredAccount is not null && triggeredBalance is decimal chosenBalance)
        {
            context["AccountId"] = triggeredAccount.AccountId.ToString(inv);
            context["AccountName"] = triggeredAccount.Name;
            context["Balance"] = chosenBalance.ToString("F2", inv);

            var fingerprint = $"AccountBalance:AccountId={triggeredAccount.AccountId}:Balance={chosenBalance.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
            var message = $"Account '{triggeredAccount.Name}' balance is {chosenBalance.ToString("N2", inv)}";

            return new AlertConditionResult(true, chosenBalance, fingerprint, message, context, OccurrenceCount: 1);
        }

        var defaultBalance = snapshot.Accounts.Count > 0 ? GetAccountBalance(snapshot.Accounts[0]) : 0m;
        var defaultFingerprint = $"AccountBalance:NoMatch:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var defaultMessage = "No account currently violates the balance condition.";

        return new AlertConditionResult(false, defaultBalance, defaultFingerprint, defaultMessage, context);
    }

    private static decimal GetAccountBalance(CurrencyAccount? account)
    {
        if (account is null || account.Entries is null || account.Entries.Count == 0)
            return 0m;

        var latestEntry = account.Entries
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.EntryId)
            .FirstOrDefault();

        return latestEntry?.Value ?? account.Entries[0].Value;
    }
}