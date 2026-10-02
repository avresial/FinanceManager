using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Administration.Models;

/// <summary>Last-rendered state of the admin dashboard today's new visitors card.</summary>
public sealed class AdminNewVisitorsTodaySnapshot : SnapshotBase
{
    /// <summary>Owner of the data. A snapshot read for a different user is rejected rather than painted.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// UTC day the count was fetched for. A snapshot from another day is rejected, so yesterday's count is
    /// never shown as today's — not even when the refresh fails.
    /// </summary>
    public DateTime Day { get; set; }

    /// <summary>The new visitors count the card last displayed.</summary>
    public int Count { get; set; }
}