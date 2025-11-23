using FinanceManager.Domain.Enums;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Entities.Bonds;

public record BondCalculationMethod
{
    public int Id { get; init; }

    [JsonIgnore]
    public BondDetails? BondDetails { get; init; }

    public DateOperator DateOperator { get; init; } = DateOperator.UntilDate;
    public string DateValue { get; init; } = string.Empty;
    public decimal Rate { get; init; }
}