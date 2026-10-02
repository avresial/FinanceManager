using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Administration.Models;

/// <summary>Last-rendered state of the admin dashboard total users count card.</summary>
public sealed class AdminUsersCountSnapshot : SnapshotBase
{
    /// <summary>Owner of the data. A snapshot read for a different user is rejected rather than painted.</summary>
    public int UserId { get; set; }

    /// <summary>The total users count the card last displayed.</summary>
    public int Count { get; set; }
}