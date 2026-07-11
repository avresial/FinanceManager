namespace FinanceManager.Application.Labels.Suggestions;

public interface ILabelSuggestionPromptProvider
{
    Task<string> BuildPromptAsync(string existingLabels, string entriesCsv, int maxSuggestions, CancellationToken cancellationToken = default);
}