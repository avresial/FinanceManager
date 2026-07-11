namespace FinanceManager.Application.Insights.Generation;

internal sealed record InsightItem(string? Title, string? Message, List<string>? Tags);