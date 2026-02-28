namespace FinanceManager.Application.Services.Ai;

public interface IInsightsPromptProvider
{
    Task<string> BuildPromptAsync(string entriesContextCsv, CancellationToken cancellationToken = default);
}