namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>How a text condition (contractor or description) matches its pattern.</summary>
public enum TextMatchOperator
{
    Equals,
    Contains,
    StartsWith,
    EndsWith,
    RegularExpression,
}