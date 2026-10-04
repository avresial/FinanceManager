using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class DashboardOverviewCardsCacheSnapshot
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public List<TimeSeriesModel> NetCashFlowSeries { get; set; } = [];
    public List<TimeSeriesModel> ClosingBalanceSeries { get; set; } = [];
}

public sealed class DashboardOverviewCardsRefreshContext
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
}