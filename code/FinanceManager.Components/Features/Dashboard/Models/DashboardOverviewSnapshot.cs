using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Persisted snapshot of the dashboard overview so the page can paint its last-rendered
/// state instantly on re-navigation, then reconcile against a fresh API response.
/// </summary>
public sealed class DashboardOverviewSnapshot : SnapshotBase
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

    public static DashboardOverviewSnapshot FromDto(DashboardOverviewDto dto) => new()
    {
        FetchedAtUtc = DateTime.UtcNow,
        UserId = dto.UserId,
        CurrencyId = dto.CurrencyId,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        NetWorthSeries = dto.NetWorthSeries,
        NetCashFlowSeries = dto.NetCashFlowSeries,
        ClosingBalanceSeries = dto.ClosingBalanceSeries,
        LiabilitiesPerType = dto.LiabilitiesPerType,
        LiabilitiesPerAccount = dto.LiabilitiesPerAccount,
        LabelsValue = dto.LabelsValue,
        AssetsPerType = dto.AssetsPerType,
        AssetsPerAccount = dto.AssetsPerAccount,
        ExpenseDistribution = dto.ExpenseDistribution,
    };

    public DashboardOverviewDto ToDto() => new()
    {
        UserId = UserId,
        CurrencyId = CurrencyId,
        StartDate = StartDate,
        EndDate = EndDate,
        NetWorthSeries = NetWorthSeries,
        NetCashFlowSeries = NetCashFlowSeries,
        ClosingBalanceSeries = ClosingBalanceSeries,
        LiabilitiesPerType = LiabilitiesPerType,
        LiabilitiesPerAccount = LiabilitiesPerAccount,
        LabelsValue = LabelsValue,
        AssetsPerType = AssetsPerType,
        AssetsPerAccount = AssetsPerAccount,
        ExpenseDistribution = ExpenseDistribution,
    };
}