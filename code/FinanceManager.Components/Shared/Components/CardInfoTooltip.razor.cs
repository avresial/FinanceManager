using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Shared.Components;

/// <summary>
/// Standard information affordance for cards: a small outlined "i" icon that opens a tooltip
/// explaining what the host card is about. The icon pins itself to the host card's top-right
/// corner (see <c>card-info-affordance.css</c>): the <c>MudCard</c> establishes the positioning
/// context and <c>.fm-card-info-anchor</c> pins the icon, so it stays in the corner no matter
/// where in the card's markup it is placed. The tooltip opens on hover and keyboard focus.
/// </summary>
public partial class CardInfoTooltip
{
    /// <summary>Concise, plain-language explanation of what the host card is about.</summary>
    [Parameter] public required string Text { get; set; }

    /// <summary>
    /// Optional rich tooltip body (a styled glossary, a list of definitions, ...). When
    /// supplied it replaces <see cref="Text"/> in the tooltip; <see cref="Text"/> must still
    /// be provided and is ignored in that case.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Accessible label announced for the icon (e.g. "About assets distribution card").
    /// Falls back to a generic label when not supplied.
    /// </summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>
    /// Tooltip placement relative to the icon. Defaults to left so a tooltip anchored to a card's
    /// top-right edge opens back into the card without collapsing against the viewport edge.
    /// </summary>
    [Parameter] public Placement Placement { get; set; } = Placement.Left;

    /// <summary>Extra CSS classes for the tooltip surface (e.g. sizing for a rich glossary).</summary>
    [Parameter] public string? TooltipClass { get; set; }

    private string ResolvedAriaLabel => string.IsNullOrWhiteSpace(AriaLabel) ? "About this card" : AriaLabel!;
}