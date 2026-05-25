using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Components.Models;

public sealed class DashboardOverviewCardsCacheSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public DateTime FetchedAtUtc { get; set; }
    public List<TimeSeriesModel> NetCashFlowSeries { get; set; } = [];
    public List<TimeSeriesModel> ClosingBalanceSeries { get; set; } = [];
    public decimal? NetWorth { get; set; }
}

public sealed class DashboardOverviewCardsRefreshContext
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
}