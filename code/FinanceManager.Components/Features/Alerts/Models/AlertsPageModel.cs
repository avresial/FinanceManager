using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Dtos;

namespace FinanceManager.Components.Features.Alerts.Models;

public sealed record AlertsPageModel(
    List<FinancialAlertDto> Alerts,
    List<AlertEvaluationOutcome> Outcomes);