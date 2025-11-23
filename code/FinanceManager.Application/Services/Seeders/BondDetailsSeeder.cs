using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class BondDetailsSeeder(IBondDetailsRepository bondDetailsRepository, ILogger<BondDetailsSeeder> logger):ISeeder
{
    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var existingBonds = await bondDetailsRepository.GetAllAsync(cancellationToken).ToListAsync(cancellationToken);
        
        if (existingBonds.Count >= 2)
        {
            logger.LogInformation("BondDetails already seeded with {Count} bonds. Skipping.", existingBonds.Count);
            return;
        }

        var bonds = new[]
        {
            new BondDetails
            {
                Name = "EDO0728",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2024, 7, 1),
                EndEmissionDate = new DateOnly(2028, 7, 1),
                Type = BondType.InflationBond
            },
            new BondDetails
            {
                Name = "EDO0428",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2024, 4, 1),
                EndEmissionDate = new DateOnly(2028, 4, 1),
                Type = BondType.InflationBond
            }
        };

        foreach (var bond in bonds)
        {
            await bondDetailsRepository.AddAsync(bond, cancellationToken);
            logger.LogInformation("Seeded bond: {Name} ({Issuer})", bond.Name, bond.Issuer);
        }

        logger.LogInformation("BondDetails seeding completed. Added {Count} bonds.", bonds.Length);
    }
}