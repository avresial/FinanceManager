using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>
/// Matches the user-visible contractor/merchant text. The default (case-insensitive
/// contains) is the most common real-world shape, e.g. matching "acme" inside
/// "ACME Corp. - March invoice".
/// </summary>
public sealed record ContractorCondition : ITransactionRuleCondition
{
    public ContractorCondition(string pattern, TextMatchOperator matchOperator = TextMatchOperator.Contains, bool ignoreCase = true)
    {
        TextMatching.ThrowIfInvalidPattern(pattern, matchOperator, ignoreCase);

        Pattern = pattern;
        MatchOperator = matchOperator;
        IgnoreCase = ignoreCase;
    }

    // Explicit constructor (not a primary constructor) so the parameterless
    // constructor can assign defaults independently of the validating overload.
    // Required for JSON deserialization.
    public ContractorCondition()
    {
        Pattern = string.Empty;
        MatchOperator = TextMatchOperator.Contains;
        IgnoreCase = true;
    }

    public string Pattern { get; init; }
    public TextMatchOperator MatchOperator { get; init; }
    public bool IgnoreCase { get; init; }

    public bool Matches(TransactionFacts facts) =>
        TextMatching.Matches(facts.Contractor, Pattern, MatchOperator, IgnoreCase);
}