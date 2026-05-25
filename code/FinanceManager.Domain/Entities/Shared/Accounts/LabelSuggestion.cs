namespace FinanceManager.Domain.Entities.Shared.Accounts;

public record LabelSuggestion(string Name, string Rationale, IReadOnlyList<string> ExampleDescriptions);