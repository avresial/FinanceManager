using FinanceManager.Domain.Enums;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Entities.Bonds;

public record BondDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateOnly StartEmissionDate { get; set; }
    public DateOnly EndEmissionDate { get; set; }
    public BondType Type { get; set; } = BondType.InflationBond;

    public List<BondCalculationMethod> CalculationMethods { get; set; } = [];

    public BondDetails()
    {
    }

    [JsonConstructor]
    public BondDetails(string name, string issuer, DateOnly startEmissionDate, DateOnly endEmissionDate,
        List<BondCalculationMethod> calculationMethods, BondType type = BondType.InflationBond)
    {
        Name = name;
        Issuer = issuer;
        StartEmissionDate = startEmissionDate;
        EndEmissionDate = endEmissionDate;
        Type = type;

        var methodsWithBackRefs = calculationMethods
            .Select(m => m with { BondDetails = this })
            .ToList();

        CalculationMethods.Clear();
        CalculationMethods.AddRange(methodsWithBackRefs);
    }
}