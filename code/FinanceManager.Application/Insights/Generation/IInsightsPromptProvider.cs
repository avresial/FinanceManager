namespace FinanceManager.Application.Insights.Generation;

public interface IInsightsPromptProvider
{
    Task<string> BuildPromptAsync(string entriesContextCsv, CancellationToken cancellationToken = default);
}