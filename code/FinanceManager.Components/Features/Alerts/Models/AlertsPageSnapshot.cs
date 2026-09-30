using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Alerts.Dtos;

namespace FinanceManager.Components.Features.Alerts.Models;

public sealed class AlertsPageSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public List<FinancialAlertDto> Alerts { get; set; } = [];
    public List<AlertEvaluationOutcome> Outcomes { get; set; } = [];
}