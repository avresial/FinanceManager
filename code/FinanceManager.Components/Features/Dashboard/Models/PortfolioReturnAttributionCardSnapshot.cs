using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class PortfolioReturnAttributionCardSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public PortfolioReturnAttributionCardModel? Model { get; set; }
}