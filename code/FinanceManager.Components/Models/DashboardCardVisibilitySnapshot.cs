namespace FinanceManager.Components.Models;

/// <summary>
/// Persisted per-user set of dashboard cards the user has chosen to hide. Stored in browser
/// local storage through <c>ISnapshotService</c>, so preferences survive reloads on the same
/// browser but are not synced across devices.
/// </summary>
public sealed class DashboardCardVisibilitySnapshot : SnapshotBase
{
    /// <summary>Ids (see <c>DashboardCards</c>) of the cards the user has explicitly hidden.</summary>
    public List<string> HiddenCardIds { get; set; } = [];
}