using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class BondDetailsSeeder(IBondDetailsRepository bondDetailsRepository, ICurrencyRepository currencyRepository, ILogger<BondDetailsSeeder> logger) : ISeeder
{
    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var existingBondNames = await bondDetailsRepository.GetAllAsync(cancellationToken)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);
        var plnCurrency = await currencyRepository.GetByCode("PLN", cancellationToken) ?? await currencyRepository.GetOrAdd("PLN", "zł", cancellationToken);
        var bonds = new[]
        {
            new BondDetails
            {
                Name = "EDO0728",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2024, 7, 1),
                EndEmissionDate = new DateOnly(2034, 7, 1),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2034-07-01",
                        Rate = 0.025m
                    }
                ]
            },
            new BondDetails
            {
                Name = "EDO0428",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2024, 4, 1),
                EndEmissionDate = new DateOnly(2034, 4, 1),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2034-04-01",
                        Rate = 0.025m
                    }
                ]
            }
        };

        var seededCount = 0;
        foreach (var bond in bonds)
        {
            if (existingBondNames.Contains(bond.Name, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogInformation("Bond {Name} already exists. Skipping.", bond.Name);
                continue;
            }

            await bondDetailsRepository.AddAsync(bond, cancellationToken);
            seededCount++;
            logger.LogInformation("Seeded bond: {Name} ({Issuer})", bond.Name, bond.Issuer);
        }

        logger.LogInformation("BondDetails seeding completed. Added {Count} bonds.", seededCount);
    }
}