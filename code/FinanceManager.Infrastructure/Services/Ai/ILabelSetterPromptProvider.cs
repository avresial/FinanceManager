namespace FinanceManager.Infrastructure.Services.Ai;

internal interface ILabelSetterPromptProvider
{
    Task<string> BuildPromptAsync(string availableLabels, string entriesCsv, CancellationToken cancellationToken = default);
}
