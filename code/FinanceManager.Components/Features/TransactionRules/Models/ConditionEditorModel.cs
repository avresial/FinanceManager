using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Components.Features.TransactionRules.Models;

/// <summary>Mutable form model for one rule condition; converts to and from the API DTO.</summary>
internal sealed class ConditionEditorModel
{
    public string Type { get; set; } = "Contractor";
    public string? Pattern { get; set; }
    public TextMatchOperator MatchOperator { get; set; } = TextMatchOperator.Contains;
    public bool IgnoreCase { get; set; } = true;
    public IReadOnlyCollection<int> AccountIds { get; set; } = [];
    public TransactionDirection Direction { get; set; } = TransactionDirection.Expense;
    public decimal? Threshold { get; set; }
    public AmountComparison Comparison { get; set; } = AmountComparison.GreaterThan;
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public static ConditionEditorModel FromDto(TransactionRuleConditionDto source) => new()
    {
        Type = source.Type,
        Pattern = source.Pattern,
        MatchOperator = source.MatchOperator,
        IgnoreCase = source.IgnoreCase,
        AccountIds = source.AccountIds,
        Direction = source.Direction,
        Threshold = source.Threshold,
        Comparison = source.Comparison,
        MinAmount = source.MinAmount,
        MaxAmount = source.MaxAmount
    };

    public TransactionRuleConditionDto ToDto()
    {
        var condition = new TransactionRuleConditionDto
        {
            Type = Type,
            Pattern = Pattern,
            MatchOperator = MatchOperator,
            IgnoreCase = IgnoreCase,
            AccountIds = Type.Equals("Account", StringComparison.OrdinalIgnoreCase) ? AccountIds.Distinct().ToList() : [],
            Direction = Direction,
            Threshold = Threshold,
            Comparison = Comparison,
            MinAmount = MinAmount,
            MaxAmount = MaxAmount
        };

        if (Type.Equals("Amount", StringComparison.OrdinalIgnoreCase) && condition.MinAmount is null &&
            condition.MaxAmount is null && condition.Threshold is null)
            condition.Threshold = 0m;

        return condition;
    }
}