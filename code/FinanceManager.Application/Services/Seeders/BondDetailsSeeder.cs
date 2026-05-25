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
                Name = "EDO1235",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2025, 12, 1),
                EndEmissionDate = new DateOnly(2035, 12, 1),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                UnitValue = 100m,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2035-12-01",
                        Rate = 0.05m
                    }
                ]
            },
            new BondDetails
            {
                Name = "EDO1033",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2023, 10, 1),
                EndEmissionDate = new DateOnly(2023, 10, 31),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                UnitValue = 100m,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2033-10-31",
                        Rate = 0.05m
                    }
                ]
            },
            new BondDetails
            {
                Name = "EDO0933",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2023, 9, 1),
                EndEmissionDate = new DateOnly(2023, 9, 30),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                UnitValue = 100m,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2033-09-30",
                        Rate = 0.05m
                    }
                ]
            },
            new BondDetails
            {
                Name = "EDO0833",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2023, 8, 1),
                EndEmissionDate = new DateOnly(2023, 8, 31),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                UnitValue = 100m,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2033-08-31",
                        Rate = 0.05m
                    }
                ]
            },

            new BondDetails
            {
                Name = "EDO0433",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2021, 4, 1),
                EndEmissionDate = new DateOnly(2021, 4, 30),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                UnitValue = 100m,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2031-04-30",
                        Rate = 0.05m
                    }
                ]
            },
            new BondDetails
            {
                Name = "EDO0233",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2021, 2, 1),
                EndEmissionDate = new DateOnly(2021, 2, 28),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                UnitValue = 100m,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2031-02-28",
                        Rate = 0.05m
                    }
                ]
            },
            new BondDetails
            {
                Name = "EDO0133",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2023, 1, 1),
                EndEmissionDate = new DateOnly(2023, 1, 31),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                UnitValue = 100m,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2033-01-31",
                        Rate = 0.05m
                    }
                ]
            },
             new BondDetails
            {
                Name = "EDO1032",
                Issuer = "Ministry of Finance - Poland",
                StartEmissionDate = new DateOnly(2022, 10, 1),
                EndEmissionDate = new DateOnly(2022, 10, 31),
                Type = BondType.InflationBond,
                Currency = plnCurrency,
                UnitValue = 100m,
                CalculationMethods =
                [
                    new ()
                    {
                        DateOperator = DateOperator.UntilDate,
                        DateValue = "2032-10-31",
                        Rate = 0.05m
                    }
                ]
            },

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