using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules;

/// <summary>
/// The complete, deterministic result of running an ordered rule set against one
/// transaction: the final facts, the per-rule trail (for previews), and which
/// rule stopped processing, if any.
/// </summary>
public class TransactionRuleEngineResult(
    TransactionFacts originalFacts,
    TransactionFacts finalFacts,
    IReadOnlyList<TransactionRuleOutcome> ruleOutcomes,
    Guid? stoppedRuleId)
{
    // Required for JSON deserialization.
    public TransactionRuleEngineResult() : this(TransactionFacts.Empty, TransactionFacts.Empty, [], null)
    {
    }

    /// <summary>The facts exactly as they were passed in (never mutated by the engine).</summary>
    public TransactionFacts OriginalFacts { get; set; } = originalFacts;

    /// <summary>The facts after every rule ran (or up to the rule that stopped processing).</summary>
    public TransactionFacts FinalFacts { get; set; } = finalFacts;

    /// <summary>
    /// One outcome per rule, in evaluation order, including disabled and
    /// non-matching rules so a preview can show the whole decision trail.
    /// </summary>
    public IReadOnlyList<TransactionRuleOutcome> RuleOutcomes { get; set; } = ruleOutcomes;

    /// <summary>The id of the rule whose <c>StopProcessing</c> flag ended the chain, or null when every rule was evaluated.</summary>
    public Guid? StoppedRuleId { get; set; } = stoppedRuleId;

    /// <summary>True when any rule's actions changed at least one field of the facts.</summary>
    public bool HasChanges { get; set; }

    /// <summary>The outcomes of the rules that actually applied actions, in order.</summary>
    public IReadOnlyList<TransactionRuleOutcome> AppliedRules =>
        RuleOutcomes.Where(outcome =>
            outcome.Status is TransactionRuleOutcomeStatus.Applied or TransactionRuleOutcomeStatus.StoppedProcessing).ToList();

    /// <summary>True when a rule stopped processing before the last rule was evaluated.</summary>
    public bool IsStopped => StoppedRuleId.HasValue;
}