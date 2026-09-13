using FinanceManager.Application.Alerts.Models;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Tests.Unit.Application.Alerts;

[Trait("Category", "Unit")]
public class FinancialAlertEvaluatorTests
{
    private readonly FinancialAlertEvaluator _evaluator = new();
    private readonly DateTime _evaluationDate = new(2026, 9, 11, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_DisabledAlert_ReturnsSuppressedWithDisabledStatus()
    {
        var alert = new FinancialAlert(1, "Disabled Alert", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m)
        {
            IsEnabled = false
        };

        var snapshot = new AlertEvaluationSnapshot([], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.False(outcome.IsTriggered);
        Assert.False(outcome.IsNewlyTriggered);
        Assert.True(outcome.IsSuppressed);
        Assert.Equal(DeDuplicationReason.AlertDisabled, outcome.DeDuplicationReason);
        Assert.Equal(AlertTriggerStatus.Disabled, outcome.Status);
    }

    [Fact]
    public void Evaluate_AccountBalance_TriggersWhenBalanceViolatesThreshold()
    {
        var alert = new FinancialAlert(1, "Low Cash", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 3000m, accountId: 10);

        var account = new CurrencyAccount(1, 10, "Checking Account", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(10, 1, _evaluationDate.AddDays(-2), 2500m, 2500m));

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.True(outcome.IsNewlyTriggered);
        Assert.False(outcome.IsSuppressed);
        Assert.Equal(AlertTriggerStatus.Triggered, outcome.Status);
        Assert.Equal(2500m, outcome.CurrentValue);
        Assert.Contains("2,500.00", outcome.Message);
    }

    [Fact]
    public void Evaluate_AccountBalance_RemainsHealthyWhenBalanceSatisfiesThreshold()
    {
        var alert = new FinancialAlert(1, "Low Cash", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 3000m, accountId: 10);

        var account = new CurrencyAccount(1, 10, "Checking Account", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(10, 1, _evaluationDate.AddDays(-2), 4500m, 4500m));

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.False(outcome.IsTriggered);
        Assert.False(outcome.IsNewlyTriggered);
        Assert.False(outcome.IsSuppressed);
        Assert.Equal(AlertTriggerStatus.Healthy, outcome.Status);
        Assert.Equal(4500m, outcome.CurrentValue);
    }

    [Fact]
    public void Evaluate_AccountBalance_WithoutAccountId_EvaluatesAcrossAllAccounts()
    {
        var alert = new FinancialAlert(1, "Any Low Account", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m);

        var safeAccount = new CurrencyAccount(1, 1, "Safe Account", AccountLabel.Cash);
        safeAccount.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddDays(-2), 5000m, 5000m));

        var lowAccount = new CurrencyAccount(1, 2, "Low Account", AccountLabel.Cash);
        lowAccount.Add(new CurrencyAccountEntry(2, 1, _evaluationDate.AddDays(-2), 400m, 400m));

        var snapshot = new AlertEvaluationSnapshot([safeAccount, lowAccount], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.Equal(400m, outcome.CurrentValue);
        Assert.Equal("2", outcome.Context["AccountId"]);
    }

    [Fact]
    public void Evaluate_CategorySpending_TriggersWhenSpendingExceedsThresholdInPeriod()
    {
        var diningLabel = new FinancialLabel { Id = 5, Name = "Restaurants" };
        var alert = new FinancialAlert(1, "Restaurants Limit", AlertType.CategorySpending, AlertComparisonOperator.GreaterThan, 1000m,
            evaluationPeriod: AlertEvaluationPeriod.CurrentMonth,
            labelId: 5);

        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        // Transaction inside current month (September 2026): expense of 600
        account.Add(new CurrencyAccountEntry(1, 1, new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc), 1400m, -600m)
        {
            Labels = [diningLabel]
        });
        // Transaction inside current month: expense of 547
        account.Add(new CurrencyAccountEntry(1, 2, new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc), 853m, -547m)
        {
            Labels = [diningLabel]
        });
        // Transaction outside current month (August 2026): expense of 800 (should be excluded)
        account.Add(new CurrencyAccountEntry(1, 3, new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), 2000m, -800m)
        {
            Labels = [diningLabel]
        });

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.True(outcome.IsNewlyTriggered);
        Assert.Equal(1147m, outcome.CurrentValue);
        Assert.Equal("2", outcome.Context["TransactionCount"]);
    }

    [Fact]
    public void Evaluate_CategorySpending_MatchesByLabelName_IgnoresIncomeTransactions()
    {
        var groceriesLabel = new FinancialLabel { Id = 99, Name = "Groceries" };
        var alert = new FinancialAlert(1, "Groceries Limit", AlertType.CategorySpending, AlertComparisonOperator.GreaterThan, 300m,
            labelName: "groceries");

        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        // Expense of 250
        account.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddDays(-2), 750m, -250m)
        {
            Labels = [groceriesLabel]
        });
        // Positive return / refund of 100 (should not count as spending)
        account.Add(new CurrencyAccountEntry(1, 2, _evaluationDate.AddDays(-1), 850m, 100m)
        {
            Labels = [groceriesLabel]
        });

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.False(outcome.IsTriggered);
        Assert.Equal(250m, outcome.CurrentValue);
    }

    [Fact]
    public void Evaluate_MerchantSpending_MatchesContractorOrDescriptionAndTriggers()
    {
        var alert = new FinancialAlert(1, "Uber Spend", AlertType.MerchantSpending, AlertComparisonOperator.GreaterThan, 150m,
            merchantName: "Uber");

        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        // Matched via ContractorDetails
        account.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddDays(-3), 900m, -95m)
        {
            ContractorDetails = "Uber B.V. Amsterdam"
        });
        // Matched via Description
        account.Add(new CurrencyAccountEntry(1, 2, _evaluationDate.AddDays(-1), 820m, -80m)
        {
            Description = "UBER TRIP HELP.UBER.COM"
        });
        // Different merchant
        account.Add(new CurrencyAccountEntry(1, 3, _evaluationDate.AddDays(-1), 720m, -100m)
        {
            ContractorDetails = "Starbucks"
        });

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.Equal(175m, outcome.CurrentValue);
        Assert.Equal("2", outcome.Context["TransactionCount"]);
    }

    [Fact]
    public void Evaluate_LargeTransaction_TriggersOnSingleExpenseExceedingThreshold()
    {
        var alert = new FinancialAlert(1, "Large Spend", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThanOrEqual, 2000m);

        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddDays(-2), 5000m, -2500m)
        {
            ContractorDetails = "Apple Store"
        });
        account.Add(new CurrencyAccountEntry(1, 2, _evaluationDate.AddDays(-1), 4700m, -300m));

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.True(outcome.IsNewlyTriggered);
        Assert.Equal(2500m, outcome.CurrentValue);
        Assert.Equal("1", outcome.Context["TriggeringAccountId"]);
        Assert.Equal("1", outcome.Context["TriggeringEntryId"]);
    }

    [Fact]
    public void Evaluate_LargeTransaction_HistoricalImportProtection_DoesNotTriggerOnOldTransactions()
    {
        // Alert was created today with AllTime period
        var alert = new FinancialAlert(1, "Large Spend", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThanOrEqual, 2000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime)
        {
            CreatedAt = _evaluationDate
        };

        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        // Old transaction from 6 months ago (historical import before alert was created)
        account.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddMonths(-6), 10000m, -4000m));

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.False(outcome.IsTriggered);
        Assert.Equal(0m, outcome.CurrentValue);
    }

    [Fact]
    public void Evaluate_SubscriptionPriceChange_TriggersWhenSubscriptionIncreases()
    {
        var alert = new FinancialAlert(1, "Sub Price Hike", AlertType.SubscriptionPriceChange, AlertComparisonOperator.GreaterThan, 0m);

        var subscriptionId = Guid.NewGuid();
        var subscription = new RecurringTransactionResult("Netflix", 65m)
        {
            PatternId = subscriptionId,
            LastAmount = 65m,
            PreviousAmount = 49m,
            LastChargeDate = _evaluationDate.AddDays(-5)
        };

        var snapshot = new AlertEvaluationSnapshot([], [subscription], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.True(outcome.IsNewlyTriggered);
        Assert.Equal(16m, outcome.CurrentValue);
        Assert.Equal("Netflix", outcome.Context["SubscriptionName"]);
        Assert.Contains("49.00", outcome.Message);
        Assert.Contains("65.00", outcome.Message);
    }

    [Fact]
    public void Evaluate_SubscriptionPriceChange_DoesNotTriggerWhenPriceIsUnchanged()
    {
        var alert = new FinancialAlert(1, "Sub Price Hike", AlertType.SubscriptionPriceChange, AlertComparisonOperator.GreaterThan, 0m);

        var subscription = new RecurringTransactionResult("Spotify", 30m)
        {
            PatternId = Guid.NewGuid(),
            LastAmount = 30m,
            PreviousAmount = 30m,
            LastChargeDate = _evaluationDate.AddDays(-2)
        };

        var snapshot = new AlertEvaluationSnapshot([], [subscription], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.False(outcome.IsTriggered);
        Assert.Equal(0m, outcome.CurrentValue);
    }

    [Fact]
    public void Evaluate_SubscriptionPriceChange_IgnoresInitialDetectionWithoutPriorCharge()
    {
        var alert = new FinancialAlert(1, "Sub Price Hike", AlertType.SubscriptionPriceChange, AlertComparisonOperator.GreaterThan, 0m);

        var newSubscription = new RecurringTransactionResult("New Service", 50m)
        {
            PatternId = Guid.NewGuid(),
            LastAmount = 50m,
            PreviousAmount = 0m,
            LastChargeDate = _evaluationDate.AddDays(-1)
        };

        var snapshot = new AlertEvaluationSnapshot([], [newSubscription], _evaluationDate);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.False(outcome.IsTriggered);
    }

    [Fact]
    public void Evaluate_DeDuplication_SuppressesUnchangedConditionOnSubsequentRuns()
    {
        var alert = new FinancialAlert(1, "Low Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, accountId: 1);
        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddDays(-1), 600m, 600m));

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);

        // First evaluation: newly triggered
        var firstOutcome = _evaluator.Evaluate(alert, snapshot);
        Assert.True(firstOutcome.IsTriggered);
        Assert.True(firstOutcome.IsNewlyTriggered);
        Assert.False(firstOutcome.IsSuppressed);
        Assert.Equal(DeDuplicationReason.None, firstOutcome.DeDuplicationReason);

        // Simulate state recording in persistence/entity
        alert.RecordTrigger(firstOutcome.CurrentValue, firstOutcome.ConditionFingerprint, firstOutcome.EvaluatedAt);

        // Second evaluation (condition unchanged): suppressed duplicate!
        var secondOutcome = _evaluator.Evaluate(alert, snapshot);
        Assert.True(secondOutcome.IsTriggered);
        Assert.False(secondOutcome.IsNewlyTriggered);
        Assert.True(secondOutcome.IsSuppressed);
        Assert.Equal(DeDuplicationReason.UnchangedCondition, secondOutcome.DeDuplicationReason);
    }

    [Fact]
    public void Evaluate_DeDuplication_TriggersNewlyWhenConditionChanges()
    {
        var alert = new FinancialAlert(1, "Low Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, accountId: 1);
        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddDays(-2), 600m, 600m));

        var snapshot1 = new AlertEvaluationSnapshot([account], [], _evaluationDate.AddDays(-1));
        var outcome1 = _evaluator.Evaluate(alert, snapshot1);
        alert.RecordTrigger(outcome1.CurrentValue, outcome1.ConditionFingerprint, outcome1.EvaluatedAt);

        // Balance drops further to 400
        account.Add(new CurrencyAccountEntry(1, 2, _evaluationDate, 400m, -200m));
        var snapshot2 = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcome2 = _evaluator.Evaluate(alert, snapshot2);

        Assert.True(outcome2.IsTriggered);
        Assert.True(outcome2.IsNewlyTriggered);
        Assert.False(outcome2.IsSuppressed);
        Assert.Equal(400m, outcome2.CurrentValue);
    }

    [Fact]
    public void Evaluate_Cooldown_SuppressesDuplicateWhenWithinCooldownWindow()
    {
        var alert = new FinancialAlert(1, "Low Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, accountId: 1)
        {
            CooldownPeriod = TimeSpan.FromHours(24)
        };

        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddHours(-2), 600m, 600m));

        var snapshot1 = new AlertEvaluationSnapshot([account], [], _evaluationDate.AddHours(-2));
        var outcome1 = _evaluator.Evaluate(alert, snapshot1);
        alert.RecordTrigger(outcome1.CurrentValue, outcome1.ConditionFingerprint, outcome1.EvaluatedAt);

        // Balance changed slightly to 550, but evaluated only 1 hour later (within 24h cooldown)
        account.Add(new CurrencyAccountEntry(1, 2, _evaluationDate.AddHours(-1), 550m, -50m));
        var snapshot2 = new AlertEvaluationSnapshot([account], [], _evaluationDate.AddHours(-1));
        var outcome2 = _evaluator.Evaluate(alert, snapshot2);

        Assert.True(outcome2.IsTriggered);
        Assert.False(outcome2.IsNewlyTriggered);
        Assert.True(outcome2.IsSuppressed);
        Assert.Equal(DeDuplicationReason.CooldownActive, outcome2.DeDuplicationReason);
    }

    [Fact]
    public void EvaluateAll_HandlesMultipleAlertsDeterministically()
    {
        var alert1 = new FinancialAlert(1, "Low Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, accountId: 1);
        var alert2 = new FinancialAlert(1, "Large Spend", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThan, 500m);

        var account = new CurrencyAccount(1, 1, "Main", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _evaluationDate.AddDays(-1), 800m, -700m));

        var snapshot = new AlertEvaluationSnapshot([account], [], _evaluationDate);
        var outcomes = _evaluator.EvaluateAll([alert1, alert2], snapshot);

        Assert.Equal(2, outcomes.Count);
        Assert.True(outcomes[0].IsTriggered); // Balance 800 < 1000
        Assert.True(outcomes[1].IsTriggered); // Spend 700 > 500
    }
}