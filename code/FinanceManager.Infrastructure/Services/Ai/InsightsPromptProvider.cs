namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class InsightsPromptProvider : IInsightsPromptProvider
{
    private const string PromptFileRelativePath = "Prompts\\financial-insights-prompt.txt";
    private const string CsvPlaceholder = "{entries_context_csv}";

    private readonly Lazy<Task<string>> _templateTask = new(LoadTemplateAsync);

    public async Task<string> BuildPromptAsync(string entriesContextCsv, CancellationToken cancellationToken = default)
    {
        var template = await _templateTask.Value;
        return template.Replace(CsvPlaceholder, entriesContextCsv, StringComparison.Ordinal);
    }

    private static async Task<string> LoadTemplateAsync()
    {
        var promptFilePath = Path.Combine(AppContext.BaseDirectory, PromptFileRelativePath);
        if (!File.Exists(promptFilePath))
            throw new FileNotFoundException($"Insights prompt file not found at '{promptFilePath}'.");

        return await File.ReadAllTextAsync(promptFilePath);
    }
}