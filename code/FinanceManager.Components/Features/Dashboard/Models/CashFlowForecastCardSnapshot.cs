using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class CashFlowForecastCardSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public int HorizonDays { get; set; }
    public CashFlowForecastCardModel? Model { get; set; }
}