namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// The 12-column grid widths a dashboard card works at: it is laid out at
/// <paramref name="Preferred"/> and may only be widened up to <paramref name="Max"/> to fill a row.
/// Narrow cards (e.g. a distribution pie) cap <paramref name="Max"/> so they are never stretched
/// to a width they were not designed for.
/// </summary>
public sealed record DashboardCardSpan(int Preferred, int Max);