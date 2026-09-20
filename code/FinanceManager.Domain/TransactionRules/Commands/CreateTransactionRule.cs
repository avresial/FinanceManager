using FinanceManager.Domain.TransactionRules.Dtos;

namespace FinanceManager.Domain.TransactionRules.Commands;

public sealed record CreateTransactionRule(
    string Name,
    IReadOnlyList<TransactionRuleConditionDto> Conditions,
    IReadOnlyList<TransactionRuleActionDto> Actions,
    bool IsEnabled = true,
    bool StopProcessing = false);