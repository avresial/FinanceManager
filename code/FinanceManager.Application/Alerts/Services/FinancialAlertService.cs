using FinanceManager.Application.Alerts.Models;
using FinanceManager.Application.Alerts.Services.Conditions;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Alerts.Models;
using FinanceManager.Domain.Alerts.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.Shared.Services;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Alerts.Services;

public class FinancialAlertService(
    IFinancialAlertRepository alertRepository,
    ICashTransactionReader cashTransactionReader,
    ICurrencyRepository currencyRepository,
    IRecurringTransactionDetectorService recurringTransactionDetectorService,
    IFinancialAlertEvaluator evaluator,
    IDateTimeProvider dateTimeProvider,
    ILogger<FinancialAlertService> logger) : IFinancialAlertService
{
    private const int _recentEntriesPageSize = 500;

    public Task<IReadOnlyList<FinancialAlert>> GetAlertsAsync(int userId, CancellationToken cancellationToken = default) =>
        alertRepository.GetAlertsByUserId(userId, cancellationToken);

    public Task<FinancialAlert?> GetAlertByIdAsync(int userId, Guid alertId, CancellationToken cancellationToken = default) =>
        alertRepository.GetById(userId, alertId, cancellationToken);

    public async Task<FinancialAlert> CreateAlertAsync(
        int userId,
        CreateFinancialAlert command,
        CancellationToken cancellationToken = default)
    {
        return await alertRepository.Add(FinancialAlert.FromCommand(userId, command), cancellationToken);
    }

    public async Task<FinancialAlert?> UpdateAlertAsync(
        int userId,
        Guid alertId,
        UpdateFinancialAlert command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var alert = await alertRepository.GetById(userId, alertId, cancellationToken);
        if (alert is null) return null;

        alert.UpdateFrom(command);
        await alertRepository.Update(alert, cancellationToken);

        return alert;
    }

    public Task<bool> DeleteAlertAsync(int userId, Guid alertId, CancellationToken cancellationToken = default) =>
        alertRepository.Delete(userId, alertId, cancellationToken);

    public async Task<string?> ValidateCurrencyAsync(
        AlertType alertType,
        int? currencyId,
        CancellationToken cancellationToken = default)
    {
        if (!alertType.RequiresCurrency())
            return currencyId is null ? null : "Subscription alerts do not use a currency.";

        if (currencyId is not int id)
            return "Select a currency for this alert.";

        return await currencyRepository.GetCurrency(id, cancellationToken) is null
            ? "The selected currency does not exist."
            : null;
    }

    public async Task<SetAlertEnabledStatus> SetEnabledAsync(
        int userId,
        Guid alertId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var alert = await alertRepository.GetById(userId, alertId, cancellationToken);
        if (alert is null) return SetAlertEnabledStatus.NotFound;

        // Migrated cash alerts come back disabled without a currency, or with one copied from a legacy account that has no
        // currency foreign key. Enabling goes through the same currency check as saving, so neither can be switched on.
        if (isEnabled && alert.AlertType.RequiresCurrency())
        {
            if (alert.CurrencyId is null) return SetAlertEnabledStatus.CurrencyRequired;
            if (await ValidateCurrencyAsync(alert.AlertType, alert.CurrencyId, cancellationToken) is not null)
                return SetAlertEnabledStatus.CurrencyInvalid;
        }

        alert.IsEnabled = isEnabled;
        alert.UpdatedAt = dateTimeProvider.UtcNow;
        await alertRepository.Update(alert, cancellationToken);

        return SetAlertEnabledStatus.Updated;
    }

    public async Task<IReadOnlyList<AlertEvaluationOutcome>> EvaluateAlertsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var alerts = await alertRepository.GetAlertsByUserId(userId, cancellationToken);
            if (alerts.Count == 0) return [];

            var snapshot = await BuildSnapshotAsync(userId, alerts, cancellationToken);
            var outcomes = evaluator.EvaluateAll(alerts, snapshot);

            foreach (var outcome in outcomes)
            {
                var alert = alerts.FirstOrDefault(a => a.Id == outcome.AlertId);
                if (alert is null) continue;

                if (outcome.TriggeredAt is DateTime triggeredAt)
                {
                    alert.RecordTrigger(outcome.CurrentValue, outcome.ConditionFingerprint, triggeredAt);
                    await alertRepository.Update(alert, cancellationToken);
                }
                else if (outcome.Status != AlertTriggerStatus.Error
                    && outcome.ErrorMessage is null
                    && !outcome.IsTriggered
                    && alert.LastStatus == AlertTriggerStatus.Triggered)
                {
                    alert.RecordResolved(outcome.EvaluatedAt);
                    await alertRepository.Update(alert, cancellationToken);
                }
            }

            return outcomes;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to evaluate financial alerts for user {UserId}", userId);
            return [];
        }
    }

    public async Task<AlertEvaluationOutcome?> EvaluateAlertAsync(
        int userId,
        Guid alertId,
        CancellationToken cancellationToken = default,
        bool includeAllMatchingTransactions = false)
    {
        try
        {
            var alert = await alertRepository.GetById(userId, alertId, cancellationToken);
            if (alert is null)
            {
                return null;
            }

            var snapshot = await BuildSnapshotAsync(userId, [alert], cancellationToken, includeAllMatchingTransactions);
            var outcome = evaluator.Evaluate(alert, snapshot);

            if (outcome.TriggeredAt is DateTime triggeredAt)
            {
                alert.RecordTrigger(outcome.CurrentValue, outcome.ConditionFingerprint, triggeredAt);
                await alertRepository.Update(alert, cancellationToken);
            }
            else if (outcome.Status != AlertTriggerStatus.Error
                && outcome.ErrorMessage is null
                && !outcome.IsTriggered
                && alert.LastStatus == AlertTriggerStatus.Triggered)
            {
                alert.RecordResolved(outcome.EvaluatedAt);
                await alertRepository.Update(alert, cancellationToken);
            }

            return outcome;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to evaluate financial alert {AlertId} for user {UserId}", alertId, userId);
            return null;
        }
    }

    private async Task<AlertEvaluationSnapshot> BuildSnapshotAsync(
        int userId,
        IReadOnlyList<FinancialAlert> alerts,
        CancellationToken cancellationToken,
        bool includeAllMatchingTransactions = false)
    {
        var now = dateTimeProvider.UtcNow;
        var enabled = alerts.Where(alert => alert.IsEnabled).ToList();
        var cashAlerts = enabled.Where(alert => alert.AlertType.RequiresCurrency()).ToList();

        var allTimeAlerts = cashAlerts
            .Where(alert => alert.EvaluationPeriod == AlertEvaluationPeriod.AllTime && IsSpendingOrLarge(alert.AlertType))
            .ToList();
        var allTimeEvaluationData = allTimeAlerts.Count == 0
            ? new Dictionary<Guid, FinancialAlertEvaluationData>()
            : await alertRepository.GetAllTimeEvaluationData(
                userId,
                allTimeAlerts,
                now,
                cancellationToken,
                includeAllMatchingTransactions);

        var cashPositions = cashAlerts.Any(alert => alert.AlertType == AlertType.AccountBalance)
            ? await cashTransactionReader.GetPositions(userId, now, cancellationToken)
            : [];

        var boundedAlerts = cashAlerts
            .Where(alert => AlertPeriod.IsBounded(alert.EvaluationPeriod) && IsSpendingOrLarge(alert.AlertType))
            .ToList();
        var recentEntries = boundedAlerts.Count == 0
            ? []
            : await LoadRecentEntriesAsync(
                userId,
                boundedAlerts.Min(alert => AlertPeriod.GetRange(alert.EvaluationPeriod, now).Start),
                now,
                cancellationToken);

        var currencyCodes = new Dictionary<int, string>();
        if (cashAlerts.Count > 0)
        {
            await foreach (var currency in currencyRepository.GetCurrencies(cancellationToken).WithCancellation(cancellationToken))
                currencyCodes[currency.Id] = currency.ShortName;
        }

        var subscriptions = enabled.Any(alert => alert.AlertType == AlertType.SubscriptionPriceChange)
            ? (await recurringTransactionDetectorService.GetRecurringTransactions(userId, cancellationToken))
                .Where(x => !x.IsIncome)
                .ToList()
            : [];

        return new AlertEvaluationSnapshot
        {
            CashPositions = cashPositions,
            CurrencyCodes = currencyCodes,
            RecentEntries = recentEntries,
            Subscriptions = subscriptions,
            AllTimeEvaluationData = allTimeEvaluationData,
            EvaluationDate = now,
            IncludeAllMatchingTransactions = includeAllMatchingTransactions
        };
    }

    private static bool IsSpendingOrLarge(AlertType type) =>
        type is AlertType.CategorySpending or AlertType.MerchantSpending or AlertType.LargeTransaction;

    // The longest bounded period is the current month (at most 31 days), so this stays a short, keyset-paged read.
    private async Task<IReadOnlyList<CashLogEntry>> LoadRecentEntriesAsync(
        int userId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var entries = new List<CashLogEntry>();
        OwnerHistoryBoundary? boundary = null;
        while (true)
        {
            var page = await cashTransactionReader.GetHistory(
                userId,
                new OwnerHistoryQuery(start, end, _recentEntriesPageSize, boundary, null, null, IncludeDetails: true),
                cancellationToken);
            entries.AddRange(page);
            if (page.Count < _recentEntriesPageSize) return entries;

            var last = page[^1];
            boundary = new OwnerHistoryBoundary(last.PostingDate, last.EntryId, IncludeEqualId: false);
        }
    }
}