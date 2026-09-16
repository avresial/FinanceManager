namespace FinanceManager.Domain.TransactionRules;

/// <summary>
/// What one rule did to a transaction, as a preview-friendly per-rule entry:
/// the <see cref="Status"/> plus the resulting values of the fields this rule
/// changed (<c>null</c> for a field the rule left untouched) and the resulting
/// label list after the rule ran (<c>null</c> when the rule changed no labels).
/// </summary>
public sealed record TransactionRuleOutcome(
    Guid RuleId,
    string RuleName,
    TransactionRuleOutcomeStatus Status,
    bool HasChanges,
    string? Contractor,
    string? Description,
    IReadOnlyList<string>? Labels,
    bool StoppedProcessing)
{
    // Required for JSON deserialization.
    public TransactionRuleOutcome() : this(Guid.Empty, string.Empty, TransactionRuleOutcomeStatus.NotMatched, false, null, null, null, false)
    {
    }
}