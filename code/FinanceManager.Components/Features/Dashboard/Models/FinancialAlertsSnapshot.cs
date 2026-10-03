using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>Last-rendered Financial Alerts summary, scoped to the user it was saved for.</summary>
public sealed class FinancialAlertsSnapshot : SnapshotBase
{
    public int UserId { get; set; }

    public FinancialAlertsSummaryModel Summary { get; set; } = new(0, [], false);
}