using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Application.Alerts.Services;

public interface IFinancialAlertService
{
    Task<IReadOnlyList<FinancialAlert>> GetAlertsAsync(int userId, CancellationToken cancellationToken = default);
    Task<FinancialAlert?> GetAlertByIdAsync(int userId, Guid alertId, CancellationToken cancellationToken = default);
    Task<FinancialAlert> CreateAlertAsync(int userId, CreateFinancialAlert command, CancellationToken cancellationToken = default);
    Task<FinancialAlert?> UpdateAlertAsync(int userId, Guid alertId, UpdateFinancialAlert command, CancellationToken cancellationToken = default);
    Task<bool> DeleteAlertAsync(int userId, Guid alertId, CancellationToken cancellationToken = default);
    /// <summary>The one rule for an alert's currency: cash alerts need an existing currency, subscription alerts must have none. Null when valid.</summary>
    Task<string?> ValidateCurrencyAsync(AlertType alertType, int? currencyId, CancellationToken cancellationToken = default);
    Task<SetAlertEnabledStatus> SetEnabledAsync(int userId, Guid alertId, bool isEnabled, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertEvaluationOutcome>> EvaluateAlertsAsync(int userId, CancellationToken cancellationToken = default);
    Task<AlertEvaluationOutcome?> EvaluateAlertAsync(
        int userId,
        Guid alertId,
        CancellationToken cancellationToken = default,
        bool includeAllMatchingTransactions = false);
}