using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Components.Features.TransactionRules.Services;

/// <summary>Human-readable summaries of rules, conditions, actions and preview outcomes.</summary>
internal static class TransactionRuleText
{
    public static string DescribeConditions(TransactionRuleDto rule, TransactionRuleAccountLabels accounts) => rule.Conditions.Count == 0
        ? "Every transaction"
        : string.Join(" and ", rule.Conditions.Select(condition => DescribeCondition(condition, false, accounts)));

    public static string DescribeCondition(TransactionRuleConditionDto condition, bool includeOptions, TransactionRuleAccountLabels accounts) => condition.Type.ToLowerInvariant() switch
    {
        "contractor" or "description" => $"{condition.Type} {DescribeMatch(condition.MatchOperator)} “{condition.Pattern}”{(includeOptions ? condition.IgnoreCase ? " · ignore case" : " · case sensitive" : string.Empty)}",
        "account" => $"Account is {string.Join(" or ", condition.AccountIds.Select(accounts.Get))}",
        "direction" => $"Direction is {condition.Direction}",
        "amount" => DescribeAmount(condition),
        _ => condition.Type
    };

    public static string DescribeComparison(AmountComparison comparison) => comparison switch
    {
        AmountComparison.LessThan => "less than",
        AmountComparison.LessThanOrEqual => "at most",
        AmountComparison.Equal => "equals",
        AmountComparison.GreaterThanOrEqual => "at least",
        AmountComparison.GreaterThan => "greater than",
        _ => comparison.ToString()
    };

    public static string DescribeActions(TransactionRuleDto rule) => rule.Actions.Count == 0
        ? "No changes"
        : string.Join("; ", rule.Actions.Select(DescribeAction));

    public static string DescribeAction(TransactionRuleActionDto action) => action.Type.ToLowerInvariant() switch
    {
        "setlabels" or "labels" => $"{(action.ReplaceExisting ? "Replace existing labels with" : "Add labels")} {FormatLabels(action.Labels)}",
        "normalizecontractor" or "contractor" => $"Rename contractor to “{action.Value}”",
        "normalizedescription" or "description" => $"Set description to “{action.Value}”",
        _ => action.Type
    };

    public static string DescribeOutcome(TransactionRuleOutcome outcome) => outcome.Status switch
    {
        TransactionRuleOutcomeStatus.SkippedDisabled => "Disabled · not evaluated",
        TransactionRuleOutcomeStatus.NotMatched => "No match",
        TransactionRuleOutcomeStatus.SkippedAfterStop => "Skipped after an earlier rule stopped processing",
        TransactionRuleOutcomeStatus.StoppedProcessing => $"Matched · {(outcome.HasChanges ? "changed transaction" : "no effective change")} · stopped after actions",
        _ => $"Matched · {(outcome.HasChanges ? "changed transaction" : "no effective change")}"
    };

    public static string DisplayText(string? value) => string.IsNullOrEmpty(value) ? "None" : value;

    public static string FormatLabels(IReadOnlyList<string> labels) => labels.Count == 0
        ? "None"
        : string.Join(", ", labels);

    private static string DescribeAmount(TransactionRuleConditionDto condition)
    {
        if (condition.Threshold is decimal threshold)
            return $"{condition.Direction} amount {DescribeComparison(condition.Comparison)} {MoneyFormatter.FormatNumber(threshold)}";
        var bounds = new List<string>();
        if (condition.MinAmount is decimal min) bounds.Add($"at least {MoneyFormatter.FormatNumber(min)}");
        if (condition.MaxAmount is decimal max) bounds.Add($"at most {MoneyFormatter.FormatNumber(max)}");
        return $"{condition.Direction} amount {string.Join(" and ", bounds)} (inclusive)";
    }

    private static string DescribeMatch(TextMatchOperator match) => match switch
    {
        TextMatchOperator.Equals => "equals",
        TextMatchOperator.Contains => "contains",
        TextMatchOperator.StartsWith => "starts with",
        TextMatchOperator.EndsWith => "ends with",
        TextMatchOperator.RegularExpression => "matches regular expression",
        _ => match.ToString()
    };
}