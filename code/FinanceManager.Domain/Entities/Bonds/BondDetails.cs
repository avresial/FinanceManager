using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Enums;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Entities.Bonds;

public class BondDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateOnly StartEmissionDate { get; set; }
    public DateOnly EndEmissionDate { get; set; }
    public BondType Type { get; set; } = BondType.InflationBond;
    public Currency Currency { get; set; } = DefaultCurrency.PLN;
    public List<BondCalculationMethod> CalculationMethods { get; set; } = [];
    public static Capitalization Capitalization => Capitalization.Annual;

    public BondDetails()
    {
    }

    [JsonConstructor]
    public BondDetails(string name, string issuer, DateOnly startEmissionDate, DateOnly endEmissionDate,
        List<BondCalculationMethod> calculationMethods, Currency? currency = null, BondType type = BondType.InflationBond)
    {
        Name = name;
        Issuer = issuer;
        StartEmissionDate = startEmissionDate;
        EndEmissionDate = endEmissionDate;
        Type = type;
        Currency = currency ?? DefaultCurrency.PLN;

        var methodsWithBackRefs = calculationMethods
            .Select(m => m with { BondDetails = this })
            .ToList();

        CalculationMethods.Clear();
        CalculationMethods.AddRange(methodsWithBackRefs);
    }

    public bool IsActiveAt(DateOnly date) => date >= StartEmissionDate && date <= EndEmissionDate;
}