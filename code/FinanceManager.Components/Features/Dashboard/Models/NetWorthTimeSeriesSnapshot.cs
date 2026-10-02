using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class NetWorthTimeSeriesSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TimeSeriesModel> Series { get; set; } = [];
}