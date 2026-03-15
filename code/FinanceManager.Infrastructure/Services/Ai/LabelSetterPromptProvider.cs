using FinanceManager.Application.Services.Ai;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class LabelSetterPromptProvider : ILabelSetterPromptProvider
{
    private const string _promptFileRelativePath = "Prompts\\label-setter-prompt.txt";
    private const string _availableLabelsPlaceholder = "{available_labels}";
    private const string _entriesCsvPlaceholder = "{entries_csv}";

    private readonly Lazy<Task<string>> _templateTask = new(LoadTemplateAsync);

    public async Task<string> BuildPromptAsync(string availableLabels, string entriesCsv, CancellationToken cancellationToken = default)
    {
        var template = await _templateTask.Value;
        return template
            .Replace(_availableLabelsPlaceholder, availableLabels, StringComparison.Ordinal)
            .Replace(_entriesCsvPlaceholder, entriesCsv, StringComparison.Ordinal);
    }

    private static async Task<string> LoadTemplateAsync()
    {
        var promptFilePath = Path.Combine(AppContext.BaseDirectory, _promptFileRelativePath);
        if (!File.Exists(promptFilePath))
            throw new FileNotFoundException($"Label setter prompt file not found at '{promptFilePath}'.");

        return await File.ReadAllTextAsync(promptFilePath);
    }
}
