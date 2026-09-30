using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.MoneyFlow.Models;

public sealed class CashFlowForecastPageSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public int HorizonDays { get; set; }
    public CashFlowForecastPageModel? Model { get; set; }
}