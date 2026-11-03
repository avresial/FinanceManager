using System.Text.Json;

namespace FinanceManager.Components.Features.Alerts.Models;

/// <summary>
/// Compares only what the alerts page renders, so a refresh that changes nothing visible does not
/// repaint or re-write the snapshot.
/// </summary>
internal sealed class AlertsPageContentComparer : IEqualityComparer<AlertsPageModel>
{
    public static readonly AlertsPageContentComparer Instance = new();

    public bool Equals(AlertsPageModel? x, AlertsPageModel? y) =>
        ReferenceEquals(x, y) || x is not null && y is not null && RenderedContent(x) == RenderedContent(y);

    public int GetHashCode(AlertsPageModel model) => RenderedContent(model).GetHashCode(StringComparison.Ordinal);

    private static string RenderedContent(AlertsPageModel model) => JsonSerializer.Serialize(new
    {
        model.Alerts,
        Outcomes = model.Outcomes.Select(outcome => new
        {
            outcome.AlertId,
            outcome.Status,
            outcome.IsTriggered,
            outcome.CurrentValue,
            outcome.Message,
            outcome.MatchingTransactions,
            outcome.MatchingTransactionCount,
        })
    });
}