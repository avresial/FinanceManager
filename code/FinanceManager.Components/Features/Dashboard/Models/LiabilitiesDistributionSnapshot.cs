using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Persisted snapshot of the liabilities distribution card's self-loaded state so it can paint its
/// last-rendered breakdown instantly on re-navigation, then reconcile against a fresh API response.
/// </summary>
/// <remarks>
/// Only the self-loading path is snapshotted; when the dashboard supplies a prepared model the card
/// renders it directly and the dashboard overview owns that snapshot. Scoped to the owning user and
/// preferred currency id; the captured range is stored for context but is not part of the key.
/// </remarks>
public sealed class LiabilitiesDistributionSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<NameValueResult> TypeData { get; set; } = [];
    public List<NameValueResult> AccountData { get; set; } = [];
}