using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Alerts.Models;
using FinanceManager.Domain.Alerts.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.Shared.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Alerts.Services;

public class FinancialAlertService(
    IFinancialAlertRepository alertRepository,
    IFinancialAccountRepository accountRepository,
    IRecurringTransactionDetectorService recurringTransactionDetectorService,
    IFinancialAlertEvaluator evaluator,
    IDateTimeProvider dateTimeProvider,
    ILogger<FinancialAlertService> logger) : IFinancialAlertService
{
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

    public async Task<bool> SetEnabledAsync(
        int userId,
        Guid alertId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var alert = await alertRepository.GetById(userId, alertId, cancellationToken);
        if (alert is null) return false;

        alert.IsEnabled = isEnabled;
        alert.UpdatedAt = dateTimeProvider.UtcNow;
        await alertRepository.Update(alert, cancellationToken);

        return true;
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var alert = await alertRepository.GetById(userId, alertId, cancellationToken);
            if (alert is null)
            {
                return null;
            }

            var snapshot = await BuildSnapshotAsync(userId, [alert], cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var start = now.Date.AddMonths(-12);
        var end = now.Date.AddDays(1);

        var allTimeAlerts = alerts
            .Where(alert => alert.EvaluationPeriod == AlertEvaluationPeriod.AllTime
                && alert.AlertType is AlertType.CategorySpending or AlertType.MerchantSpending or AlertType.LargeTransaction)
            .ToList();
        var allTimeEvaluationData = allTimeAlerts.Count == 0
            ? new Dictionary<Guid, FinancialAlertEvaluationData>()
            : await alertRepository.GetAllTimeEvaluationData(userId, allTimeAlerts, now, cancellationToken);

        var accounts = new List<CurrencyAccount>();
        await foreach (var account in accountRepository.GetAccounts<CurrencyAccount>(userId, start, end).WithCancellation(cancellationToken))
        {
            accounts.Add(account);
        }

        var subscriptions = await recurringTransactionDetectorService.GetRecurringTransactions(userId, cancellationToken);

        return new AlertEvaluationSnapshot(accounts, subscriptions, now)
        {
            AllTimeEvaluationData = allTimeEvaluationData
        };
    }
}