using FinanceManager.Application.Alerts.Models;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.Alerts.Services.Conditions;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Alerts.Models;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Tests.Unit.Application.Alerts;

[Trait("Category", "Unit")]
public class FinancialAlertEvaluatorTests
{
    private const int _pln = 0;
    private const int _eur = 1;

    private readonly FinancialAlertEvaluator _evaluator = new(
    [
        new CashBalanceConditionEvaluator(),
        new CategorySpendingConditionEvaluator(),
        new MerchantSpendingConditionEvaluator(),
        new LargeTransactionConditionEvaluator(),
        new SubscriptionPriceChangeConditionEvaluator(),
    ]);
    private readonly DateTime _evaluationDate = new(2026, 9, 11, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_DisabledAlert_ReturnsSuppressedWithDisabledStatus()
    {
        var alert = new FinancialAlert(1, "Disabled Alert", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m)
        {
            IsEnabled = false
        };

        var outcome = _evaluator.Evaluate(alert, Snapshot());

        Assert.False(outcome.IsTriggered);
        Assert.Null(outcome.TriggeredAt);
        Assert.True(outcome.IsSuppressed);
        Assert.Equal(DeDuplicationReason.AlertDisabled, outcome.DeDuplicationReason);
        Assert.Equal(AlertTriggerStatus.Disabled, outcome.Status);
    }

    [Theory]
    [InlineData(AlertType.AccountBalance)]
    [InlineData(AlertType.CategorySpending)]
    [InlineData(AlertType.MerchantSpending)]
    [InlineData(AlertType.LargeTransaction)]
    public void Evaluate_EnabledCashAlertWithoutCurrency_ReportsAnError(AlertType type)
    {
        var alert = new FinancialAlert(1, "No currency", type, AlertComparisonOperator.GreaterThan, 10m);

        var outcome = _evaluator.Evaluate(alert, Snapshot());

        Assert.Equal(AlertTriggerStatus.Error, outcome.Status);
        Assert.False(outcome.IsTriggered);
        Assert.Null(outcome.TriggeredAt);
    }

    [Fact]
    public void Evaluate_AccountBalance_TriggersOnThePooledBalanceOfTheCurrency()
    {
        var alert = new FinancialAlert(1, "Low Cash", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 3000m, currencyId: _eur);

        var snapshot = Snapshot(balances: [(_eur, 2500m), (_pln, 100m)], currencyCodes: new() { [_eur] = "EUR", [_pln] = "PLN" });
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.Equal(_evaluationDate, outcome.TriggeredAt);
        Assert.False(outcome.IsSuppressed);
        Assert.Equal(AlertTriggerStatus.Triggered, outcome.Status);
        Assert.Equal(2500m, outcome.CurrentValue);
        Assert.Equal("Cash balance (EUR) is 2,500.00", outcome.Message);
        Assert.Equal("EUR", outcome.Context["CurrencyCode"]);
        Assert.Equal("AccountBalance:CurrencyId=1:Balance=2500.00:Op=LessThan:Threshold=3000.00", outcome.ConditionFingerprint);
    }

    [Fact]
    public void Evaluate_AccountBalance_RemainsHealthyWhenBalanceSatisfiesThreshold()
    {
        var alert = new FinancialAlert(1, "Low Cash", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 3000m, currencyId: _eur);

        var outcome = _evaluator.Evaluate(alert, Snapshot(balances: [(_eur, 4500m)]));

        Assert.False(outcome.IsTriggered);
        Assert.Null(outcome.TriggeredAt);
        Assert.False(outcome.IsSuppressed);
        Assert.Equal(AlertTriggerStatus.Healthy, outcome.Status);
        Assert.Equal(4500m, outcome.CurrentValue);
    }

    [Fact]
    public void Evaluate_AccountBalance_PlnIsCurrencyZero()
    {
        var alert = new FinancialAlert(-3, "Guest low", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, currencyId: _pln);

        var outcome = _evaluator.Evaluate(alert, Snapshot(balances: [(_pln, 400m), (_eur, 50_000m)], currencyCodes: new() { [_pln] = "PLN" }));

        Assert.True(outcome.IsTriggered);
        Assert.Equal(400m, outcome.CurrentValue);
        Assert.Contains("(PLN)", outcome.Message);
    }

    [Fact]
    public void Evaluate_AccountBalance_CurrencyWithoutEntries_HasAZeroBalance()
    {
        var alert = new FinancialAlert(1, "Empty", AlertType.AccountBalance, AlertComparisonOperator.LessThanOrEqual, 0m, currencyId: 7);

        var outcome = _evaluator.Evaluate(alert, Snapshot(balances: [(_pln, 100m)]));

        Assert.True(outcome.IsTriggered);
        Assert.Equal(0m, outcome.CurrentValue);
        Assert.Contains("Currency #7", outcome.Message);
    }

    [Theory]
    [InlineData(AlertType.CategorySpending)]
    [InlineData(AlertType.MerchantSpending)]
    [InlineData(AlertType.LargeTransaction)]
    public void Evaluate_UnsupportedStoredPeriod_ReportsAnErrorInsteadOfWideningToAllTime(AlertType type)
    {
        var alert = new FinancialAlert(1, "Malformed", type, AlertComparisonOperator.GreaterThan, 10m,
            evaluationPeriod: (AlertEvaluationPeriod)99, currencyId: _pln);

        var outcome = _evaluator.Evaluate(alert, Snapshot(entries: [Cash(1, new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc), -600m)]));

        Assert.Equal(AlertTriggerStatus.Error, outcome.Status);
        Assert.False(outcome.IsTriggered);
    }

    [Fact]
    public void Evaluate_CategorySpending_TriggersWhenSpendingExceedsThresholdInPeriod()
    {
        var diningLabel = new FinancialLabel { Id = 5, Name = "Restaurants" };
        var alert = new FinancialAlert(1, "Restaurants Limit", AlertType.CategorySpending, AlertComparisonOperator.GreaterThan, 1000m,
            evaluationPeriod: AlertEvaluationPeriod.CurrentMonth,
            currencyId: _pln,
            labelId: 5);

        var snapshot = Snapshot(entries:
        [
            // Inside the current month (September 2026)
            Cash(1, new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc), -600m, labels: [diningLabel]),
            Cash(2, new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc), -547m, labels: [diningLabel]),
            // Outside the current month (August 2026), excluded
            Cash(3, new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), -800m, labels: [diningLabel]),
            // Another currency, excluded
            Cash(4, new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc), -900m, _eur, labels: [diningLabel]),
        ]);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.Equal(_evaluationDate, outcome.TriggeredAt);
        Assert.Equal(1147m, outcome.CurrentValue);
        Assert.Equal("2", outcome.Context["TransactionCount"]);
        Assert.Equal(2, outcome.MatchingTransactionCount);
        Assert.Equal([1, 2], outcome.MatchingTransactions!.Select(transaction => transaction.EntryId).Order());
        Assert.StartsWith("CategorySpending:CurrencyId=0:Label=5:", outcome.ConditionFingerprint);
        Assert.DoesNotContain("threshold", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_CategorySpending_DetailedSnapshotIncludesEveryMatchingTransaction()
    {
        var label = new FinancialLabel { Id = 5, Name = "Restaurants" };
        var alert = new FinancialAlert(
            1,
            "Restaurants limit",
            AlertType.CategorySpending,
            AlertComparisonOperator.GreaterThan,
            100m,
            currencyId: _pln,
            labelId: 5);
        var entries = Enumerable.Range(1, 6)
            .Select(entryId => Cash(entryId, _evaluationDate.AddDays(-entryId), -entryId * 100m, labels: [label]))
            .ToList();

        var summaryOutcome = _evaluator.Evaluate(alert, Snapshot(entries: entries));
        var detailedOutcome = _evaluator.Evaluate(alert, Snapshot(entries: entries, includeAll: true));

        Assert.Equal(6, summaryOutcome.MatchingTransactionCount);
        Assert.Equal(5, summaryOutcome.MatchingTransactions!.Count);
        Assert.Equal(6, detailedOutcome.MatchingTransactions!.Count);
        Assert.Equal(6, detailedOutcome.OccurrenceCount);
    }

    [Fact]
    public void Evaluate_CategorySpending_MatchesByLabelName_IgnoresIncomeTransactions()
    {
        var groceriesLabel = new FinancialLabel { Id = 99, Name = "Groceries" };
        var alert = new FinancialAlert(1, "Groceries Limit", AlertType.CategorySpending, AlertComparisonOperator.GreaterThan, 300m,
            currencyId: _pln,
            labelName: "groceries");

        var snapshot = Snapshot(entries:
        [
            Cash(1, _evaluationDate.AddDays(-2), -250m, labels: [groceriesLabel]),
            // A refund is not spending
            Cash(2, _evaluationDate.AddDays(-1), 100m, labels: [groceriesLabel]),
        ]);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.False(outcome.IsTriggered);
        Assert.Equal(250m, outcome.CurrentValue);
    }

    [Fact]
    public void Evaluate_AllTimeCategorySpending_UsesAggregatedEvaluationData()
    {
        var alert = new FinancialAlert(
            1,
            "All dining",
            AlertType.CategorySpending,
            AlertComparisonOperator.GreaterThan,
            1000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime,
            currencyId: _pln,
            labelName: "Dining");
        var snapshot = Snapshot(allTime: new Dictionary<Guid, FinancialAlertEvaluationData>
        {
            [alert.Id] = new FinancialAlertEvaluationData(1250m, 3, null)
        });

        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.Equal(1250m, outcome.CurrentValue);
        Assert.Equal("3", outcome.Context["TransactionCount"]);
    }

    [Fact]
    public void Evaluate_MerchantSpending_MatchesContractorOrDescriptionAndTriggers()
    {
        var alert = new FinancialAlert(1, "Uber Spend", AlertType.MerchantSpending, AlertComparisonOperator.GreaterThan, 150m,
            currencyId: _pln,
            merchantName: "Uber");

        var snapshot = Snapshot(entries:
        [
            Cash(1, _evaluationDate.AddDays(-3), -95m, contractor: "Uber B.V. Amsterdam"),
            Cash(2, _evaluationDate.AddDays(-1), -80m, description: "UBER TRIP HELP.UBER.COM"),
            Cash(3, _evaluationDate.AddDays(-1), -100m, contractor: "Starbucks"),
            Cash(4, _evaluationDate.AddDays(-1), -500m, _eur, contractor: "Uber Eats"),
        ]);
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.Equal(175m, outcome.CurrentValue);
        Assert.Equal("2", outcome.Context["TransactionCount"]);
        Assert.Equal(2, outcome.MatchingTransactionCount);
        Assert.Equal([1, 2], outcome.MatchingTransactions!.Select(transaction => transaction.EntryId).Order());
        Assert.StartsWith("MerchantSpending:CurrencyId=0:Merchant=uber:", outcome.ConditionFingerprint);
    }

    [Fact]
    public void Evaluate_LargeTransaction_TriggersOnSingleExpenseExceedingThreshold()
    {
        var alert = new FinancialAlert(1, "Large Spend", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThanOrEqual, 2000m, currencyId: _pln);

        var snapshot = Snapshot(
            entries:
            [
                Cash(1, _evaluationDate.AddDays(-2), -2500m, contractor: "Apple Store"),
                Cash(2, _evaluationDate.AddDays(-1), -300m),
                Cash(3, _evaluationDate.AddDays(-1), -9000m, _eur),
            ],
            currencyCodes: new() { [_pln] = "PLN" });
        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.Equal(_evaluationDate, outcome.TriggeredAt);
        Assert.Equal(2500m, outcome.CurrentValue);
        Assert.Equal("0", outcome.Context["CurrencyId"]);
        Assert.Equal("1", outcome.Context["TriggeringEntryId"]);
        Assert.False(outcome.Context.ContainsKey("TriggeringAccountId"));
        Assert.Equal(1, outcome.MatchingTransactionCount);
        var reference = outcome.MatchingTransactions!.Single();
        Assert.Equal(1, reference.EntryId);
        Assert.Equal("PLN", reference.CurrencyCode);
        Assert.Equal("Large transaction detected on 2026-09-09", outcome.Message);
        Assert.StartsWith("LargeTransaction:CurrencyId=0:Entries=1:", outcome.ConditionFingerprint);
    }

    [Fact]
    public void Evaluate_LargeTransaction_FingerprintListsEntryIdsNumerically()
    {
        var alert = new FinancialAlert(1, "Large Spend", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThan, 100m, currencyId: _pln);

        var outcome = _evaluator.Evaluate(alert, Snapshot(entries:
        [
            Cash(9, _evaluationDate.AddDays(-1), -300m),
            Cash(10, _evaluationDate.AddDays(-2), -200m),
            Cash(100, _evaluationDate.AddDays(-3), -150m),
        ]));

        Assert.Equal("LargeTransaction:CurrencyId=0:Entries=9,10,100:Max=300.00:Op=GreaterThan:Threshold=100.00", outcome.ConditionFingerprint);
        Assert.Equal(9, outcome.MatchingTransactions![0].EntryId);
    }

    [Fact]
    public void Evaluate_AllTimeLargeTransaction_UsesAggregatedDataAndAFingerprintWithoutAccounts()
    {
        var alert = new FinancialAlert(1, "Large all time", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThan, 100m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime, currencyId: _eur)
        {
            CreatedAt = _evaluationDate.AddDays(-30)
        };
        var largest = Cash(42, _evaluationDate.AddDays(-1), -750m, _eur);
        var snapshot = Snapshot(
            allTime: new Dictionary<Guid, FinancialAlertEvaluationData>
            {
                [alert.Id] = new FinancialAlertEvaluationData(0m, 2, largest, [largest])
            },
            currencyCodes: new() { [_eur] = "EUR" });

        var outcome = _evaluator.Evaluate(alert, snapshot);

        Assert.True(outcome.IsTriggered);
        Assert.Equal("LargeTransaction:CurrencyId=1:Count=2:Top=42:Max=750.00:Op=GreaterThan:Threshold=100.00", outcome.ConditionFingerprint);
        Assert.Equal("EUR", Assert.Single(outcome.MatchingTransactions!).CurrencyCode);
    }

    [Fact]
    public void Evaluate_LargeTransaction_HistoricalImportProtection_DoesNotTriggerOnOldTransactions()
    {
        // Alert was created today with AllTime period
        var alert = new FinancialAlert(1, "Large Spend", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThanOrEqual, 2000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime, currencyId: _pln)
        {
            CreatedAt = _evaluationDate
        };

        // Old transaction from 6 months ago (historical import before alert was created)
        var snapshot = Snapshot(entries: [Cash(1, _evaluationDate.AddMonths(-6), -4000m)]);
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

        var outcome = _evaluator.Evaluate(alert, Snapshot(subscriptions: [subscription]));

        Assert.True(outcome.IsTriggered);
        Assert.Equal(_evaluationDate, outcome.TriggeredAt);
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

        var outcome = _evaluator.Evaluate(alert, Snapshot(subscriptions: [subscription]));

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

        var outcome = _evaluator.Evaluate(alert, Snapshot(subscriptions: [newSubscription]));

        Assert.False(outcome.IsTriggered);
    }

    [Fact]
    public void Evaluate_DeDuplication_SuppressesUnchangedConditionOnSubsequentRuns()
    {
        var alert = new FinancialAlert(1, "Low Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, currencyId: _pln);
        var snapshot = Snapshot(balances: [(_pln, 600m)]);

        // First evaluation: newly triggered
        var firstOutcome = _evaluator.Evaluate(alert, snapshot);
        Assert.True(firstOutcome.IsTriggered);
        Assert.Equal(_evaluationDate, firstOutcome.TriggeredAt);
        Assert.False(firstOutcome.IsSuppressed);
        Assert.Equal(DeDuplicationReason.None, firstOutcome.DeDuplicationReason);

        // Simulate state recording in persistence/entity
        alert.RecordTrigger(firstOutcome.CurrentValue, firstOutcome.ConditionFingerprint, firstOutcome.EvaluatedAt);

        // Second evaluation (condition unchanged): suppressed duplicate!
        var secondOutcome = _evaluator.Evaluate(alert, snapshot);
        Assert.True(secondOutcome.IsTriggered);
        Assert.Null(secondOutcome.TriggeredAt);
        Assert.True(secondOutcome.IsSuppressed);
        Assert.Equal(DeDuplicationReason.UnchangedCondition, secondOutcome.DeDuplicationReason);
    }

    [Fact]
    public void Evaluate_DeDuplication_TriggersNewlyWhenConditionChanges()
    {
        var alert = new FinancialAlert(1, "Low Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, currencyId: _pln);

        var outcome1 = _evaluator.Evaluate(alert, Snapshot(balances: [(_pln, 600m)], at: _evaluationDate.AddDays(-1)));
        alert.RecordTrigger(outcome1.CurrentValue, outcome1.ConditionFingerprint, outcome1.EvaluatedAt);

        // Balance drops further to 400
        var outcome2 = _evaluator.Evaluate(alert, Snapshot(balances: [(_pln, 400m)]));

        Assert.True(outcome2.IsTriggered);
        Assert.Equal(_evaluationDate, outcome2.TriggeredAt);
        Assert.False(outcome2.IsSuppressed);
        Assert.Equal(400m, outcome2.CurrentValue);
    }

    [Fact]
    public void Evaluate_DeDuplication_FreshReEnabledAlertTriggersNormally()
    {
        // A migrated alert comes back with no fingerprint, so the first evaluation after re-enabling is a normal fresh trigger.
        var alert = new FinancialAlert(1, "Migrated", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, currencyId: _pln)
        {
            IsEnabled = true,
            LastStatus = AlertTriggerStatus.Healthy,
            LastTriggeredConditionFingerprint = null
        };

        var outcome = _evaluator.Evaluate(alert, Snapshot(balances: [(_pln, 600m)]));

        Assert.Equal(_evaluationDate, outcome.TriggeredAt);
        Assert.False(outcome.IsSuppressed);
    }

    [Fact]
    public void EvaluateAll_HandlesMultipleAlertsDeterministically()
    {
        var alert1 = new FinancialAlert(1, "Low Balance", AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m, currencyId: _pln);
        var alert2 = new FinancialAlert(1, "Large Spend", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThan, 500m, currencyId: _pln);

        var snapshot = Snapshot(balances: [(_pln, 800m)], entries: [Cash(1, _evaluationDate.AddDays(-1), -700m)]);
        var outcomes = _evaluator.EvaluateAll([alert1, alert2], snapshot);

        Assert.Equal(2, outcomes.Count);
        Assert.True(outcomes[0].IsTriggered); // Balance 800 < 1000
        Assert.True(outcomes[1].IsTriggered); // Spend 700 > 500
    }

    private AlertEvaluationSnapshot Snapshot(
        IEnumerable<CashLogEntry>? entries = null,
        IEnumerable<(int CurrencyId, decimal Value)>? balances = null,
        IReadOnlyList<RecurringTransactionResult>? subscriptions = null,
        Dictionary<int, string>? currencyCodes = null,
        IReadOnlyDictionary<Guid, FinancialAlertEvaluationData>? allTime = null,
        bool includeAll = false,
        DateTime? at = null) => new()
        {
            CashPositions = [.. (balances ?? []).Select(balance => new OwnerLedgerPosition(balance.CurrencyId, balance.Value, _evaluationDate.AddDays(-2)))],
            CurrencyCodes = currencyCodes ?? [],
            RecentEntries = [.. entries ?? []],
            Subscriptions = subscriptions ?? [],
            AllTimeEvaluationData = allTime ?? new Dictionary<Guid, FinancialAlertEvaluationData>(),
            IncludeAllMatchingTransactions = includeAll,
            EvaluationDate = at ?? _evaluationDate,
        };

    private static CashLogEntry Cash(long id, DateTime postingDate, decimal change, int currencyId = _pln,
        string description = "", string? contractor = null, IReadOnlyList<FinancialLabel>? labels = null) =>
        new(id, postingDate, change, description, currencyId, null, contractor, labels ?? []);
}