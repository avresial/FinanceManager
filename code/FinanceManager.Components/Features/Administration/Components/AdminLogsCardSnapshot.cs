using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Administration.Components;

/// <summary>Last-rendered state of the admin recent warnings and errors card.</summary>
public sealed class AdminLogsCardSnapshot : SnapshotBase
{
    /// <summary>Owner of the data. A snapshot read for a different user is rejected rather than painted.</summary>
    public int UserId { get; set; }

    /// <summary>The entries the card last displayed, newest first.</summary>
    public List<AdminLogEntryView> Entries { get; set; } = [];
}