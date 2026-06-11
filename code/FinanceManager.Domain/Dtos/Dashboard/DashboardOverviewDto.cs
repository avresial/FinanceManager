using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Dtos.Dashboard;

/// <summary>
/// Dashboard-shaped read model composing the data loaded by the dashboard
/// first-paint cards into a single response, so the frontend can initialize
/// dashboard state once instead of fanning out to many card-level endpoints.
/// </summary>
public class DashboardOverviewDto
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public List<TimeSeriesModel> NetWorthSeries { get; set; } = [];
    public List<TimeSeriesModel> NetCashFlowSeries { get; set; } = [];
    public List<TimeSeriesModel> ClosingBalanceSeries { get; set; } = [];

    public List<NameValueResult> LiabilitiesPerType { get; set; } = [];
    public List<NameValueResult> LiabilitiesPerAccount { get; set; } = [];

    public List<NameValueResult> LabelsValue { get; set; } = [];

    public List<NameValueResult> AssetsPerType { get; set; } = [];
    public List<NameValueResult> AssetsPerAccount { get; set; } = [];

    public List<NameValueResult> ExpenseDistribution { get; set; } = [];
}