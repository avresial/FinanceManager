using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Application.Alerts.Services.Conditions;

/// <summary>
/// Evaluates the raw condition of one <see cref="AlertType"/> against a snapshot, without
/// any enabled/suppression/de-duplication decisions (those belong to <see cref="FinancialAlertEvaluator"/>).
/// </summary>
public interface IAlertConditionEvaluator
{
    AlertType AlertType { get; }

    AlertConditionResult Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot);
}