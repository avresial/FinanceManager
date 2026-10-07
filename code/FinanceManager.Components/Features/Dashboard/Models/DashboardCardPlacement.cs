namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Where a card lands in the 12-column grid: its <paramref name="Span"/>, and the width of the
/// filler tile rendered right after it when it closes a row its cards cannot fill (0 for none).
/// </summary>
public sealed record DashboardCardPlacement(int Span, int FillerSpanAfter);