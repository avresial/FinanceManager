using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;

namespace FinanceManager.Application.TransactionRules;

internal static class TransactionRuleCommandValidator
{
    public static void Validate(CreateTransactionRule command) =>
        Validate(command.Name, command.Conditions, command.Actions);

    public static void Validate(UpdateTransactionRule command) =>
        Validate(command.Name, command.Conditions, command.Actions);

    private static void Validate(
        string? name,
        IReadOnlyList<TransactionRuleConditionDto>? conditions,
        IReadOnlyList<TransactionRuleActionDto>? actions)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Rule name is required and must be at most 200 characters.", nameof(name));
        if (conditions is null || actions is null)
            throw new ArgumentException("Conditions and actions are required.");

        TransactionRuleRuntimeBuilder.ValidateConditions(conditions);
        TransactionRuleRuntimeBuilder.ValidateActions(actions);
    }
}