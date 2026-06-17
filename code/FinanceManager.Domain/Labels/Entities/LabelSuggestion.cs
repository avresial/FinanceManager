namespace FinanceManager.Domain.Labels.Entities;

public record LabelSuggestion(string Name, string Rationale, IReadOnlyList<string> ExampleDescriptions);