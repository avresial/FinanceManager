using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Labels.Models;

/// <summary>
/// Last-rendered state of the Subscriptions page list. Holds every detected subscription, muted and
/// cancelled ones included: the "show muted and cancelled" checkbox filters what is drawn from this
/// list, so it must not change what is stored.
/// </summary>
public sealed class SubscriptionsListSnapshot : SnapshotBase
{
    /// <summary>Owner of the data. A snapshot read for a different user is rejected rather than painted.</summary>
    public int UserId { get; set; }

    /// <summary>Currency short name the amounts were rendered with; a snapshot for another currency is rejected.</summary>
    public string Currency { get; set; } = string.Empty;

    public List<RecurringTransactionResult> Subscriptions { get; set; } = [];
}