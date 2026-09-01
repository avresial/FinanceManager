using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Shared.Components;

/// <summary>
/// Standard information tooltip for cards: the card header label itself opens a tooltip explaining
/// what the host card is about. The trigger remains in normal document flow and is keyboard
/// focusable so the header keeps its meaning and discoverability on every viewport.
/// </summary>
public partial class CardInfoTooltip
{
    /// <summary>Concise, plain-language explanation of what the host card is about.</summary>
    [Parameter] public required string Text { get; set; }

    /// <summary>
    /// Header content that should open the tooltip, such as the card's title.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Optional rich tooltip body (a styled glossary, a list of definitions, ...). When supplied
    /// it replaces <see cref="Text"/> in the tooltip body.
    /// </summary>
    [Parameter] public RenderFragment? TooltipContent { get; set; }

    /// <summary>
    /// Accessible label announced for the focusable header label (e.g. "About assets distribution card").
    /// Falls back to a generic label when not supplied.
    /// </summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>
    /// Tooltip placement relative to the header label. Defaults to bottom so the explanation reads
    /// as part of the card header without covering the card's main value.
    /// </summary>
    [Parameter] public Placement Placement { get; set; } = Placement.Bottom;

    /// <summary>Extra CSS classes for the tooltip surface (e.g. sizing for a rich glossary).</summary>
    [Parameter] public string? TooltipClass { get; set; }

    private string ResolvedAriaLabel => string.IsNullOrWhiteSpace(AriaLabel) ? "About this card" : AriaLabel!;

    private string ResolvedTooltipClass => string.IsNullOrWhiteSpace(TooltipClass)
        ? "fm-card-info-tooltip"
        : $"fm-card-info-tooltip {TooltipClass}";

    private RenderFragment TooltipBody => builder =>
    {
        var sequence = 0;
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "fm-card-info-tooltip-panel");

        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "fm-card-info-tooltip-kicker");
        builder.AddContent(sequence++, "About this card");
        builder.CloseElement();

        if (TooltipContent is null)
        {
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "fm-card-info-tooltip-copy");
            builder.AddContent(sequence++, Text);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(sequence++, TooltipContent);
        }

        builder.CloseElement();
    };
}