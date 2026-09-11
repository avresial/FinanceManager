using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using System.Globalization;

namespace FinanceManager.Application.Alerts.Services;

public class FinancialAlertEvaluator : IFinancialAlertEvaluator
{
    public AlertEvaluationOutcome Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentNullException.ThrowIfNull(snapshot);

        var evaluationTime = snapshot.EvaluationDate;

        if (!alert.IsEnabled)
        {
            return new AlertEvaluationOutcome(
                alert.Id,
                alert.Title,
                alert.AlertType,
                AlertTriggerStatus.Disabled,
                IsTriggered: false,
                IsNewlyTriggered: false,
                IsSuppressed: true,
                DeDuplicationReason.AlertDisabled,
                CurrentValue: 0m,
                alert.Threshold,
                alert.ComparisonOperator,
                ConditionFingerprint: string.Empty,
                Message: $"Alert '{alert.Title}' is disabled.",
                EvaluatedAt: evaluationTime,
                Context: new Dictionary<string, string>());
        }

        try
        {
            var rawOutcome = alert.AlertType switch
            {
                AlertType.AccountBalance => EvaluateAccountBalance(alert, snapshot),
                AlertType.CategorySpending => EvaluateCategorySpending(alert, snapshot),
                AlertType.MerchantSpending => EvaluateMerchantSpending(alert, snapshot),
                AlertType.LargeTransaction => EvaluateLargeTransaction(alert, snapshot),
                AlertType.SubscriptionPriceChange => EvaluateSubscriptionPriceChange(alert, snapshot),
                _ => throw new NotSupportedException($"Alert type '{alert.AlertType}' is not supported.")
            };

            if (!rawOutcome.ConditionMet)
            {
                return new AlertEvaluationOutcome(
                    alert.Id,
                    alert.Title,
                    alert.AlertType,
                    AlertTriggerStatus.Healthy,
                    IsTriggered: false,
                    IsNewlyTriggered: false,
                    IsSuppressed: false,
                    DeDuplicationReason.None,
                    rawOutcome.ObservedValue,
                    alert.Threshold,
                    alert.ComparisonOperator,
                    rawOutcome.Fingerprint,
                    rawOutcome.Message,
                    evaluationTime,
                    rawOutcome.Context);
            }

            // Condition is met. Check de-duplication: unchanged condition vs cooldown vs new trigger.
            if (alert.IsUnchangedTrigger(rawOutcome.Fingerprint))
            {
                return new AlertEvaluationOutcome(
                    alert.Id,
                    alert.Title,
                    alert.AlertType,
                    AlertTriggerStatus.Triggered,
                    IsTriggered: true,
                    IsNewlyTriggered: false,
                    IsSuppressed: true,
                    DeDuplicationReason.UnchangedCondition,
                    rawOutcome.ObservedValue,
                    alert.Threshold,
                    alert.ComparisonOperator,
                    rawOutcome.Fingerprint,
                    rawOutcome.Message,
                    evaluationTime,
                    rawOutcome.Context);
            }

            if (alert.IsSuppressedByCooldown(evaluationTime))
            {
                return new AlertEvaluationOutcome(
                    alert.Id,
                    alert.Title,
                    alert.AlertType,
                    AlertTriggerStatus.Triggered,
                    IsTriggered: true,
                    IsNewlyTriggered: false,
                    IsSuppressed: true,
                    DeDuplicationReason.CooldownActive,
                    rawOutcome.ObservedValue,
                    alert.Threshold,
                    alert.ComparisonOperator,
                    rawOutcome.Fingerprint,
                    rawOutcome.Message,
                    evaluationTime,
                    rawOutcome.Context);
            }

            // Newly triggered
            return new AlertEvaluationOutcome(
                alert.Id,
                alert.Title,
                alert.AlertType,
                AlertTriggerStatus.Triggered,
                IsTriggered: true,
                IsNewlyTriggered: true,
                IsSuppressed: false,
                DeDuplicationReason.None,
                rawOutcome.ObservedValue,
                alert.Threshold,
                alert.ComparisonOperator,
                rawOutcome.Fingerprint,
                rawOutcome.Message,
                evaluationTime,
                rawOutcome.Context);
        }
        catch (Exception ex)
        {
            return new AlertEvaluationOutcome(
                alert.Id,
                alert.Title,
                alert.AlertType,
                AlertTriggerStatus.Healthy,
                IsTriggered: false,
                IsNewlyTriggered: false,
                IsSuppressed: false,
                DeDuplicationReason.None,
                CurrentValue: 0m,
                alert.Threshold,
                alert.ComparisonOperator,
                ConditionFingerprint: string.Empty,
                Message: $"Evaluation failed: {ex.Message}",
                EvaluatedAt: evaluationTime,
                Context: new Dictionary<string, string>(),
                ErrorMessage: ex.ToString());
        }
    }

    public IReadOnlyList<AlertEvaluationOutcome> EvaluateAll(
        IEnumerable<FinancialAlert> alerts,
        AlertEvaluationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(snapshot);

        var outcomes = new List<AlertEvaluationOutcome>();
        foreach (var alert in alerts)
        {
            outcomes.Add(Evaluate(alert, snapshot));
        }

        return outcomes;
    }

    private static RawConditionResult EvaluateAccountBalance(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
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

            var conditionMet = MatchesComparison(balance, alert.ComparisonOperator, alert.Threshold);
            var fingerprint = $"AccountBalance:AccountId={targetAccountId}:Balance={balance.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
            var message = $"Account '{accountName}' balance is {balance.ToString("N2", inv)} (threshold: {alert.ComparisonOperator} {alert.Threshold.ToString("N2", inv)})";

            return new RawConditionResult(conditionMet, balance, fingerprint, message, context);
        }

        // AccountId is null: evaluate each available account
        CurrencyAccount? triggeredAccount = null;
        decimal? triggeredBalance = null;

        foreach (var account in snapshot.Accounts)
        {
            var balance = GetAccountBalance(account);
            if (MatchesComparison(balance, alert.ComparisonOperator, alert.Threshold))
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
            var message = $"Account '{triggeredAccount.Name}' balance is {chosenBalance.ToString("N2", inv)} (threshold: {alert.ComparisonOperator} {alert.Threshold.ToString("N2", inv)})";

            return new RawConditionResult(true, chosenBalance, fingerprint, message, context);
        }

        var defaultBalance = snapshot.Accounts.Count > 0 ? GetAccountBalance(snapshot.Accounts[0]) : 0m;
        var defaultFingerprint = $"AccountBalance:NoMatch:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var defaultMessage = $"No account violated balance threshold {alert.ComparisonOperator} {alert.Threshold.ToString("N2", inv)}";

        return new RawConditionResult(false, defaultBalance, defaultFingerprint, defaultMessage, context);
    }

    private static RawConditionResult EvaluateCategorySpending(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var (startDate, endDate) = GetPeriodRange(alert.EvaluationPeriod, snapshot.EvaluationDate);
        var periodKey = FormatPeriodKey(alert.EvaluationPeriod, startDate, endDate);
        var allEntries = snapshot.GetAllEntries();

        var qualifyingEntries = allEntries
            .Where(e => e.PostingDate >= startDate && e.PostingDate <= endDate)
            .Where(e => alert.AccountId is not int accId || e.AccountId == accId)
            .Where(e => e.ValueChange < 0)
            .Where(e =>
            {
                if (e.Labels is null || e.Labels.Count == 0) return false;
                if (alert.LabelId is int targetId)
                    return e.Labels.Any(l => l.Id == targetId);
                if (!string.IsNullOrWhiteSpace(alert.LabelName))
                    return e.Labels.Any(l => string.Equals(l.Name, alert.LabelName, StringComparison.OrdinalIgnoreCase));
                return true;
            })
            .ToList();

        var totalSpend = qualifyingEntries.Sum(e => Math.Abs(e.ValueChange));
        var labelKey = alert.LabelId?.ToString(inv) ?? alert.LabelName ?? "All";
        var conditionMet = MatchesComparison(totalSpend, alert.ComparisonOperator, alert.Threshold);

        var fingerprint = $"CategorySpending:Label={labelKey}:Period={periodKey}:Spend={totalSpend.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var message = $"Spending for category '{labelKey}' in {alert.EvaluationPeriod} is {totalSpend.ToString("N2", inv)} (threshold: {alert.ComparisonOperator} {alert.Threshold.ToString("N2", inv)})";

        var context = new Dictionary<string, string>
        {
            ["Label"] = labelKey,
            ["Period"] = periodKey,
            ["TotalSpend"] = totalSpend.ToString("F2", inv),
            ["TransactionCount"] = qualifyingEntries.Count.ToString(inv)
        };

        return new RawConditionResult(conditionMet, totalSpend, fingerprint, message, context);
    }

    private static RawConditionResult EvaluateMerchantSpending(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var (startDate, endDate) = GetPeriodRange(alert.EvaluationPeriod, snapshot.EvaluationDate);
        var periodKey = FormatPeriodKey(alert.EvaluationPeriod, startDate, endDate);
        var allEntries = snapshot.GetAllEntries();
        var merchantTarget = (alert.MerchantName ?? string.Empty).Trim();

        var qualifyingEntries = allEntries
            .Where(e => e.PostingDate >= startDate && e.PostingDate <= endDate)
            .Where(e => alert.AccountId is not int accId || e.AccountId == accId)
            .Where(e => e.ValueChange < 0)
            .Where(e =>
            {
                if (string.IsNullOrWhiteSpace(merchantTarget)) return true;
                var contractor = e.ContractorDetails ?? string.Empty;
                var desc = e.Description ?? string.Empty;
                return contractor.Contains(merchantTarget, StringComparison.OrdinalIgnoreCase)
                    || desc.Contains(merchantTarget, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var totalSpend = qualifyingEntries.Sum(e => Math.Abs(e.ValueChange));
        var conditionMet = MatchesComparison(totalSpend, alert.ComparisonOperator, alert.Threshold);

        var fingerprint = $"MerchantSpending:Merchant={merchantTarget.ToLowerInvariant()}:Period={periodKey}:Spend={totalSpend.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var message = $"Spending for merchant '{merchantTarget}' in {alert.EvaluationPeriod} is {totalSpend.ToString("N2", inv)} (threshold: {alert.ComparisonOperator} {alert.Threshold.ToString("N2", inv)})";

        var context = new Dictionary<string, string>
        {
            ["Merchant"] = merchantTarget,
            ["Period"] = periodKey,
            ["TotalSpend"] = totalSpend.ToString("F2", inv),
            ["TransactionCount"] = qualifyingEntries.Count.ToString(inv)
        };

        return new RawConditionResult(conditionMet, totalSpend, fingerprint, message, context);
    }

    private static RawConditionResult EvaluateLargeTransaction(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var (startDate, endDate) = GetPeriodRange(alert.EvaluationPeriod, snapshot.EvaluationDate);
        var allEntries = snapshot.GetAllEntries();

        // Historical import protection: for AllTime alerts, do not fire on transactions posted before alert creation
        var effectiveStart = startDate;
        if (alert.EvaluationPeriod == AlertEvaluationPeriod.AllTime && alert.CreatedAt != default)
        {
            effectiveStart = alert.CreatedAt.Date;
        }

        var qualifyingEntries = allEntries
            .Where(e => e.PostingDate >= effectiveStart && e.PostingDate <= endDate)
            .Where(e => alert.AccountId is not int accId || e.AccountId == accId)
            .Where(e => e.ValueChange < 0)
            .Where(e => MatchesComparison(Math.Abs(e.ValueChange), alert.ComparisonOperator, alert.Threshold))
            .OrderByDescending(e => Math.Abs(e.ValueChange))
            .ThenByDescending(e => e.PostingDate)
            .ToList();

        if (qualifyingEntries.Count > 0)
        {
            var largest = qualifyingEntries[0];
            var largestAmount = Math.Abs(largest.ValueChange);
            var entryIds = string.Join(",", qualifyingEntries.Select(e => $"{e.AccountId}:{e.EntryId}").Order());

            var fingerprint = $"LargeTransaction:Entries={entryIds}:Max={largestAmount.ToString("F2", inv)}:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
            var message = $"Large transaction of {largestAmount.ToString("N2", inv)} detected on {largest.PostingDate:yyyy-MM-dd} (threshold: {alert.ComparisonOperator} {alert.Threshold.ToString("N2", inv)})";

            var context = new Dictionary<string, string>
            {
                ["TriggeringAccountId"] = largest.AccountId.ToString(inv),
                ["TriggeringEntryId"] = largest.EntryId.ToString(inv),
                ["Amount"] = largestAmount.ToString("F2", inv),
                ["PostingDate"] = largest.PostingDate.ToString("yyyy-MM-dd"),
                ["ContractorDetails"] = largest.ContractorDetails ?? string.Empty,
                ["Description"] = largest.Description
            };

            return new RawConditionResult(true, largestAmount, fingerprint, message, context);
        }

        var defaultFingerprint = $"LargeTransaction:NoMatch:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var defaultMessage = $"No transaction violated threshold {alert.ComparisonOperator} {alert.Threshold.ToString("N2", inv)}";

        return new RawConditionResult(false, 0m, defaultFingerprint, defaultMessage, new Dictionary<string, string>());
    }

    private static RawConditionResult EvaluateSubscriptionPriceChange(FinancialAlert alert, AlertEvaluationSnapshot snapshot)
    {
        var inv = CultureInfo.InvariantCulture;
        var targetSubs = snapshot.Subscriptions.AsEnumerable();
        if (alert.SubscriptionId is Guid subId)
        {
            targetSubs = targetSubs.Where(s => s.PatternId == subId);
        }

        var qualifyingSubs = new List<(RecurringTransactionResult Sub, decimal PriceDelta)>();

        foreach (var sub in targetSubs)
        {
            if (sub.PreviousAmount <= 0m || sub.LastAmount == sub.PreviousAmount)
                continue;

            var delta = sub.PriceDelta;

            var matches = alert.Threshold switch
            {
                0m when alert.ComparisonOperator == AlertComparisonOperator.GreaterThan => delta > 0m,
                0m when alert.ComparisonOperator == AlertComparisonOperator.NotEqual => delta != 0m,
                _ => MatchesComparison(delta, alert.ComparisonOperator, alert.Threshold)
            };

            if (matches)
            {
                qualifyingSubs.Add((sub, delta));
            }
        }

        if (qualifyingSubs.Count > 0)
        {
            var (topSub, delta) = qualifyingSubs
                .OrderByDescending(x => Math.Abs(x.PriceDelta))
                .First();

            var subKeys = string.Join(",", qualifyingSubs.Select(s => $"{s.Sub.PatternId}:{s.Sub.PreviousAmount.ToString("F2", inv)}->{s.Sub.LastAmount.ToString("F2", inv)}:{s.Sub.LastChargeDate:yyyyMMdd}").Order());
            var fingerprint = $"SubscriptionPriceChange:Subs={subKeys}";
            var message = $"Subscription '{topSub.Name}' price changed by {delta.ToString("+0.00;-0.00", inv)} (from {topSub.PreviousAmount.ToString("N2", inv)} to {topSub.LastAmount.ToString("N2", inv)})";

            var context = new Dictionary<string, string>
            {
                ["PatternId"] = topSub.PatternId.ToString(),
                ["SubscriptionName"] = topSub.Name,
                ["PreviousAmount"] = topSub.PreviousAmount.ToString("F2", inv),
                ["LastAmount"] = topSub.LastAmount.ToString("F2", inv),
                ["PriceDelta"] = delta.ToString("F2", inv),
                ["LastChargeDate"] = topSub.LastChargeDate.ToString("yyyy-MM-dd")
            };

            return new RawConditionResult(true, delta, fingerprint, message, context);
        }

        var defaultFingerprint = $"SubscriptionPriceChange:NoMatch:Op={alert.ComparisonOperator}:Threshold={alert.Threshold.ToString("F2", inv)}";
        var defaultMessage = $"No subscription price change matched threshold {alert.ComparisonOperator} {alert.Threshold.ToString("N2", inv)}";

        return new RawConditionResult(false, 0m, defaultFingerprint, defaultMessage, new Dictionary<string, string>());
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

    private static bool MatchesComparison(decimal actual, AlertComparisonOperator op, decimal threshold) => op switch
    {
        AlertComparisonOperator.GreaterThan => actual > threshold,
        AlertComparisonOperator.GreaterThanOrEqual => actual >= threshold,
        AlertComparisonOperator.LessThan => actual < threshold,
        AlertComparisonOperator.LessThanOrEqual => actual <= threshold,
        AlertComparisonOperator.Equal => actual == threshold,
        AlertComparisonOperator.NotEqual => actual != threshold,
        _ => false
    };

    private static (DateTime Start, DateTime End) GetPeriodRange(AlertEvaluationPeriod period, DateTime evaluationDate)
    {
        return period switch
        {
            AlertEvaluationPeriod.CurrentMonth => (
                new DateTime(evaluationDate.Year, evaluationDate.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                evaluationDate),
            AlertEvaluationPeriod.Last30Days => (
                evaluationDate.Date.AddDays(-30),
                evaluationDate),
            AlertEvaluationPeriod.Last7Days => (
                evaluationDate.Date.AddDays(-7),
                evaluationDate),
            AlertEvaluationPeriod.AllTime => (
                DateTime.MinValue,
                evaluationDate),
            _ => (DateTime.MinValue, evaluationDate)
        };
    }

    private static string FormatPeriodKey(AlertEvaluationPeriod period, DateTime start, DateTime end) => period switch
    {
        AlertEvaluationPeriod.CurrentMonth => $"{start:yyyy-MM}",
        AlertEvaluationPeriod.Last30Days => $"30d-{end:yyyyMMdd}",
        AlertEvaluationPeriod.Last7Days => $"7d-{end:yyyyMMdd}",
        _ => "AllTime"
    };

    private sealed record RawConditionResult(
        bool ConditionMet,
        decimal ObservedValue,
        string Fingerprint,
        string Message,
        IReadOnlyDictionary<string, string> Context);
}