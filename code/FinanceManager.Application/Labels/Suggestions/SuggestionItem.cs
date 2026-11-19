namespace FinanceManager.Application.Labels.Suggestions;

internal sealed record SuggestionItem(string? LabelName, string? Rationale, List<string>? ExampleDescriptions);