namespace FinanceManager.Application.Services.Ai;

public interface ILabelSuggestionPromptProvider
{
    Task<string> BuildPromptAsync(string existingLabels, string entriesCsv, int maxSuggestions, CancellationToken cancellationToken = default);
}