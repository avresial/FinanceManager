namespace FinanceManager.Application.Services.Ai;

public interface ILabelSetterPromptProvider
{
    Task<string> BuildPromptAsync(string availableLabels, string entriesCsv, CancellationToken cancellationToken = default);
}
