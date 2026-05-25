using FinanceManager.Application.Services.Ai;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class InsightsPromptProvider : IInsightsPromptProvider
{
    private const string _promptFileRelativePath = "Prompts\\financial-insights-prompt.txt";
    private const string _csvPlaceholder = "{entries_context_csv}";

    private readonly Lazy<Task<string>> _templateTask = new(LoadTemplateAsync);

    public async Task<string> BuildPromptAsync(string entriesContextCsv, CancellationToken cancellationToken = default)
    {
        var template = await _templateTask.Value;
        return template.Replace(_csvPlaceholder, entriesContextCsv, StringComparison.Ordinal);
    }

    private static async Task<string> LoadTemplateAsync()
    {
        var promptFilePath = Path.Combine(AppContext.BaseDirectory, _promptFileRelativePath);
        if (!File.Exists(promptFilePath))
            throw new FileNotFoundException($"Insights prompt file not found at '{promptFilePath}'.");

        return await File.ReadAllTextAsync(promptFilePath);
    }
}