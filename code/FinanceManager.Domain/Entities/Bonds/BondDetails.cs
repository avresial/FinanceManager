using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Bonds;

public record BondDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateOnly StartEmissionDate { get; set; }
    public DateOnly EndEmissionDate { get; set; }
    public BondType Type { get; set; } = BondType.InflationBond;

    private readonly List<BondCalculationMethod> _calculationMethods = [];
    public IReadOnlyCollection<BondCalculationMethod> CalculationMethods => _calculationMethods.AsReadOnly();

    public BondDetails()
    {
    }

    public BondDetails(string name, string issuer, DateOnly startEmissionDate, DateOnly endEmissionDate,
        IList<BondCalculationMethod> calculationMethods, BondType type = BondType.InflationBond)
    {
        Name = name;
        Issuer = issuer;
        StartEmissionDate = startEmissionDate;
        EndEmissionDate = endEmissionDate;
        Type = type;

        if (calculationMethods == null || calculationMethods.Count == 0)
            throw new InvalidOperationException(nameof(calculationMethods));

        var methodsWithBackRefs = calculationMethods
            .Select(m => m with { BondDetails = this })
            .ToList();

        _calculationMethods.Clear();
        _calculationMethods.AddRange(methodsWithBackRefs);
    }
}