namespace FinanceManager.Domain.TransactionRules;

/// <summary>What the engine did with a rule while processing a single transaction.</summary>
public enum TransactionRuleOutcomeStatus
{
    /// <summary>The rule was disabled and was not evaluated.</summary>
    SkippedDisabled,

    /// <summary>The rule was evaluated and at least one condition failed.</summary>
    NotMatched,

    /// <summary>The rule matched and its actions were applied; later rules continue.</summary>
    Applied,

    /// <summary>The rule matched, its actions were applied, and it stopped the chain.</summary>
    StoppedProcessing,

    /// <summary>The rule was never evaluated because an earlier rule stopped processing.</summary>
    SkippedAfterStop,
}