namespace FinanceManager.Infrastructure.Services.Ai;

internal interface IInsightsPromptProvider
{
    Task<string> BuildPromptAsync(string entriesContextCsv, CancellationToken cancellationToken = default);
}