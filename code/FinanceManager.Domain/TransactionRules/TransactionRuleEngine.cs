using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules;

/// <summary>
/// Deterministic, sequential execution of an ordered transaction rule set against
/// a single transaction.
/// </summary>
/// <remarks>
/// Rules are evaluated in ascending <see cref="TransactionRule.Order"/> (ties keep
/// input order); a disabled rule is skipped without evaluation, a matching rule
/// applies its actions to a working copy of the facts so later rules evaluate the
/// current state and may overwrite earlier values, and a matching rule with
/// <see cref="TransactionRule.StopProcessing"/> ends the chain. The input
/// <paramref name="facts"/> is never mutated and the result is fully
/// reproducible for the same rules and facts, which makes it preview-friendly.
/// </remarks>
public static class TransactionRuleEngine
{
    /// <summary>Runs the given rules against the given facts and returns the complete, ordered result.</summary>
    public static TransactionRuleEngineResult Run(IEnumerable<TransactionRule> rules, TransactionFacts facts)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(facts);

        // OrderBy is stable, so rules sharing an Order keep their input sequence.
        var orderedRules = rules.OrderBy(rule => rule.Order).ToList();

        var working = new TransactionFacts(
            facts.Contractor,
            facts.Description,
            facts.AccountId,
            facts.Amount,
            facts.Direction,
            facts.Labels);
        var workingLabels = new List<string>(working.Labels);

        var outcomes = new List<TransactionRuleOutcome>();
        Guid? stoppedRuleId = null;

        foreach (var rule in orderedRules)
        {
            if (stoppedRuleId.HasValue)
            {
                outcomes.Add(new(rule.Id, rule.Name, TransactionRuleOutcomeStatus.SkippedAfterStop, false, null, null, null, false));
                continue;
            }

            if (!rule.IsEnabled)
            {
                outcomes.Add(new(rule.Id, rule.Name, TransactionRuleOutcomeStatus.SkippedDisabled, false, null, null, null, false));
                continue;
            }

            if (!rule.Matches(working))
            {
                outcomes.Add(new(rule.Id, rule.Name, TransactionRuleOutcomeStatus.NotMatched, false, null, null, null, false));
                continue;
            }

            var contractorBefore = working.Contractor;
            var descriptionBefore = working.Description;
            var labelsBefore = new List<string>(workingLabels);

            foreach (var action in rule.Actions)
                action.Apply(workingLabels, working);

            var contractorChanged = working.Contractor != contractorBefore;
            var descriptionChanged = working.Description != descriptionBefore;
            var labelsChanged = !workingLabels.SequenceEqual(labelsBefore);
            var stopped = rule.StopProcessing;
            if (stopped)
                stoppedRuleId = rule.Id;

            outcomes.Add(new(
                rule.Id,
                rule.Name,
                stopped ? TransactionRuleOutcomeStatus.StoppedProcessing : TransactionRuleOutcomeStatus.Applied,
                contractorChanged || descriptionChanged || labelsChanged,
                contractorChanged ? working.Contractor : null,
                descriptionChanged ? working.Description : null,
                labelsChanged ? (string[])[.. workingLabels] : null,
                stopped));
        }

        var finalFacts = new TransactionFacts(
            working.Contractor,
            working.Description,
            working.AccountId,
            working.Amount,
            working.Direction,
            workingLabels);

        return new TransactionRuleEngineResult(facts, finalFacts, outcomes, stoppedRuleId)
        {
            HasChanges = finalFacts != facts,
        };
    }
}