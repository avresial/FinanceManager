using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class SavingsRateCardSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public int HorizonMonths { get; set; }
    public DateOnly AsOfMonth { get; set; }
    public DateTime AsOfDateTime { get; set; }
    public SavingsRateCardModel? Model { get; set; }
}