using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>Matches the user-visible description text with the same operators as <see cref="ContractorCondition"/>.</summary>
public sealed record DescriptionCondition : ITransactionRuleCondition
{
    public DescriptionCondition(string pattern, TextMatchOperator matchOperator = TextMatchOperator.Contains, bool ignoreCase = true)
    {
        TextMatching.ThrowIfInvalidPattern(pattern, ignoreCase);

        Pattern = pattern;
        MatchOperator = matchOperator;
        IgnoreCase = ignoreCase;
    }

    // Explicit constructor (not a primary constructor) so the parameterless
    // constructor can assign defaults independently of the validating overload.
    // Required for JSON deserialization.
    public DescriptionCondition()
    {
        Pattern = string.Empty;
        MatchOperator = TextMatchOperator.Contains;
        IgnoreCase = true;
    }

    public string Pattern { get; init; }
    public TextMatchOperator MatchOperator { get; init; }
    public bool IgnoreCase { get; init; }

    public bool Matches(TransactionFacts facts) =>
        TextMatching.Matches(facts.Description, Pattern, MatchOperator, IgnoreCase);
}