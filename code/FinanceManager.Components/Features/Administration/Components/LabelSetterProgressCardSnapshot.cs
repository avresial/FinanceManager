using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Labels.Dtos;

namespace FinanceManager.Components.Features.Administration.Components;

/// <summary>Last-rendered state of the admin label setter progress card.</summary>
public sealed class LabelSetterProgressCardSnapshot : SnapshotBase
{
    /// <summary>Owner of the data. A snapshot read for a different user is rejected rather than painted.</summary>
    public int UserId { get; set; }

    /// <summary>The progress the card last displayed.</summary>
    public LabelSetterProgressSnapshot? Progress { get; set; }
}