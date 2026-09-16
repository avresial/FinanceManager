using FinanceManager.Domain.TransactionRules.Actions;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules;

/// <summary>
/// A single transaction automation rule: an ordered, name-identified set of
/// <see cref="Conditions"/> (all must match) and <see cref="Actions"/> (applied in
/// order to a matching transaction), plus the <see cref="IsEnabled"/> and
/// <see cref="StopProcessing"/> flags.
///
/// Rules carry no transaction or import identity: they describe *what to look for*
/// and *what to change*, never *which* transaction.
/// </summary>
public class TransactionRule(
    Guid id,
    string name,
    int order,
    IReadOnlyCollection<ITransactionRuleCondition> conditions,
    IReadOnlyCollection<ITransactionRuleAction> actions,
    bool stopProcessing = false,
    bool isEnabled = true)
{
    // Required for JSON deserialization.
    public TransactionRule() : this(Guid.Empty, string.Empty, 0, [], [], false)
    {
    }

    public Guid Id { get; set; } = id;
    public string Name { get; set; } = name;

    /// <summary>
    /// The evaluation position of this rule. Rules are applied in ascending
    /// <see cref="Order"/>; rules sharing an order keep their input sequence.
    /// </summary>
    public int Order { get; set; } = order;
    public IReadOnlyCollection<ITransactionRuleCondition> Conditions { get; set; } = conditions;
    public IReadOnlyCollection<ITransactionRuleAction> Actions { get; set; } = actions;

    /// <summary>
    /// When true, no rule after this one is evaluated once this rule matches and
    /// applies, so an earlier, more specific rule can shield later, broader ones.
    /// </summary>
    public bool StopProcessing { get; set; } = stopProcessing;

    /// <summary>When false, the rule is skipped without evaluation and has no effect.</summary>
    public bool IsEnabled { get; set; } = isEnabled;

    /// <summary>
    /// Returns true when every condition is satisfied by the given facts.
    /// A rule without conditions matches every transaction.
    /// </summary>
    public bool Matches(TransactionFacts facts) => Conditions.All(condition => condition.Matches(facts));
}