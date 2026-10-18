namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class AssetsPageCardsRefreshContext
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
}