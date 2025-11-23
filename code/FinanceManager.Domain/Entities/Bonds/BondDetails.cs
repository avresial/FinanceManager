using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities;

public record BondDetails
{
    public int Id { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public DateOnly StartEmissionDate { get; set; }
    public DateOnly EndEmissionDate { get; set; }
    public BondType Type { get; set; } = BondType.InflationBond;

    public BondDetails()
    {
    }

    public BondDetails(int id, string issuer, DateOnly startEmissionDate, DateOnly endEmissionDate, BondType type = BondType.InflationBond)
    {
        Id = id;
        Issuer = issuer;
        StartEmissionDate = startEmissionDate;
        EndEmissionDate = endEmissionDate;
        Type = type;
    }
}
