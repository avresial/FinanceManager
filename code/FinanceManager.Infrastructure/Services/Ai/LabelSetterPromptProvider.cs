namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class LabelSetterPromptProvider : ILabelSetterPromptProvider
{
    private const string PromptFileRelativePath = "Prompts\\label-setter-prompt.txt";
    private const string AvailableLabelsPlaceholder = "{available_labels}";
    private const string EntriesCsvPlaceholder = "{entries_csv}";

    private readonly Lazy<Task<string>> _templateTask = new(LoadTemplateAsync);

    public async Task<string> BuildPromptAsync(string availableLabels, string entriesCsv, CancellationToken cancellationToken = default)
    {
        var template = await _templateTask.Value;
        return template
            .Replace(AvailableLabelsPlaceholder, availableLabels, StringComparison.Ordinal)
            .Replace(EntriesCsvPlaceholder, entriesCsv, StringComparison.Ordinal);
    }

    private static async Task<string> LoadTemplateAsync()
    {
        var promptFilePath = Path.Combine(AppContext.BaseDirectory, PromptFileRelativePath);
        if (!File.Exists(promptFilePath))
            throw new FileNotFoundException($"Label setter prompt file not found at '{promptFilePath}'.");

        return await File.ReadAllTextAsync(promptFilePath);
    }
}
