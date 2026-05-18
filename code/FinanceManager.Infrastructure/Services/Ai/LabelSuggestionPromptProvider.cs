using FinanceManager.Application.Services.Ai;
using System.Globalization;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class LabelSuggestionPromptProvider : ILabelSuggestionPromptProvider
{
    private const string _promptFileRelativePath = "Prompts\\label-suggestion-prompt.txt";
    private const string _existingLabelsPlaceholder = "{existing_labels}";
    private const string _entriesCsvPlaceholder = "{entries_csv}";
    private const string _maxSuggestionsPlaceholder = "{max_suggestions}";

    private readonly Lazy<Task<string>> _templateTask = new(LoadTemplateAsync);

    public async Task<string> BuildPromptAsync(string existingLabels, string entriesCsv, int maxSuggestions, CancellationToken cancellationToken = default)
    {
        var template = await _templateTask.Value;
        return template
            .Replace(_existingLabelsPlaceholder, existingLabels, StringComparison.Ordinal)
            .Replace(_entriesCsvPlaceholder, entriesCsv, StringComparison.Ordinal)
            .Replace(_maxSuggestionsPlaceholder, maxSuggestions.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static async Task<string> LoadTemplateAsync()
    {
        var promptFilePath = Path.Combine(AppContext.BaseDirectory, _promptFileRelativePath);
        if (!File.Exists(promptFilePath))
            throw new FileNotFoundException($"Label suggestion prompt file not found at '{promptFilePath}'.");

        return await File.ReadAllTextAsync(promptFilePath);
    }
}