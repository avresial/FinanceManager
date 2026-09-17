namespace FinanceManager.Domain.TransactionRules.Dtos;

/// <summary>JSON-friendly action contract used by the API and persisted rule definitions.</summary>
public sealed class TransactionRuleActionDto
{
    public string Type { get; set; } = string.Empty;
    public string? Value { get; set; }
    public List<string> Labels { get; set; } = [];
    public bool ReplaceExisting { get; set; }
}