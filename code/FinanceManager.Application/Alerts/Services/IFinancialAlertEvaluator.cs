using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;

namespace FinanceManager.Application.Alerts.Services;

public interface IFinancialAlertEvaluator
{
    AlertEvaluationOutcome Evaluate(FinancialAlert alert, AlertEvaluationSnapshot snapshot);
    IReadOnlyList<AlertEvaluationOutcome> EvaluateAll(IEnumerable<FinancialAlert> alerts, AlertEvaluationSnapshot snapshot);
}