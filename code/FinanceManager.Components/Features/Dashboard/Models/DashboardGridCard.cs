namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>A page card's stable visibility id, menu title, and preferred large-screen width.</summary>
public sealed record DashboardGridCard(string Id, string Title, DashboardCardSpan LargeSpan);