using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Dtos;

/// <summary>
/// JSON-friendly condition contract used by the API and persisted rule definitions.
/// A condition uses only the fields relevant to its <see cref="Type"/>.
/// </summary>
public sealed class TransactionRuleConditionDto
{
    public string Type { get; set; } = string.Empty;
    public string? Pattern { get; set; }
    public TextMatchOperator MatchOperator { get; set; } = TextMatchOperator.Contains;
    public bool IgnoreCase { get; set; } = true;
    public List<int> AccountIds { get; set; } = [];
    public TransactionDirection Direction { get; set; } = TransactionDirection.Expense;
    public decimal? Threshold { get; set; }
    public AmountComparison Comparison { get; set; } = AmountComparison.GreaterThan;
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}