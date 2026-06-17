using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Shared;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Entities;

public record BondCalculationMethod
{
    public int Id { get; init; }

    [JsonIgnore]
    public BondDetails? BondDetails { get; init; }

    public DateOperator DateOperator { get; init; } = DateOperator.UntilDate;
    public string DateValue { get; init; } = string.Empty;
    public decimal Rate { get; init; }

    public bool IsActiveAt(DateOnly date)
    {
        if (BondDetails is null) return false;

        DateOnly comparisonDate = DateOnly.Parse(DateValue);

        return DateOperator switch
        {
            DateOperator.UntilDate => date <= comparisonDate,
            // DateOperator.FromDate => date >= comparisonDate,
            // DateOperator.ExactDate => date == comparisonDate,
            _ => false,
        };
    }
}