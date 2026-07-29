using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Persisted snapshot of the liabilities time-series card so it can paint its last-rendered
/// chart instantly on re-navigation, then reconcile against a fresh API response.
/// </summary>
/// <remarks>
/// Scoped to the owning user and preferred currency. The captured date range is stored for
/// context but is deliberately not part of the snapshot key — one snapshot per user/currency
/// is overwritten on each save. Content equality runs over the rendered
/// <see cref="Dashboard.Models.TimeSeriesCardModel"/>, so <see cref="SnapshotBase.FetchedAtUtc"/>
/// and the range never count as a change.
/// </remarks>
public sealed class LiabilitiesTimeSeriesSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TimeSeriesModel> Series { get; set; } = [];
}