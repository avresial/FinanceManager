using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Bond.Seeders;

public class BondAccountSeeder(
    IBondTransactionReader bondTransactionReader,
    IBondAccountEntryRepository<BondAccountEntry> bondEntryRepository,
    IBondDetailsRepository bondDetailsRepository,
    ILogger<BondAccountSeeder> logger)
{
    public async Task Seed(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (userId == 0 || await bondTransactionReader.HasAnyEntries(userId, cancellationToken)) return;

        var bondDetails = await bondDetailsRepository.GetAllAsync(cancellationToken).FirstOrDefaultAsync(cancellationToken);
        if (bondDetails is null || bondDetails.Id == 0 || bondDetails.UnitValue <= 0)
        {
            logger.LogWarning("No usable bond details available for guest {UserId}; skipping bond account seeding.", userId);
            return;
        }

        logger.LogTrace("Seeding bond entries.");
        await SeedBondEntries(userId, bondDetails, start, end, cancellationToken);
    }

    private async Task SeedBondEntries(int userId, BondDetails bondDetails, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        // Each month buys the fractional number of units the recurring bond investment pays for, so the value
        // of the units acquired matches the cash outflow CurrencyAccountSeeder books on the same day.
        var unitsPerPurchase = Math.Round(GuestInvestmentPlan.BondMonthlyAmount / bondDetails.UnitValue, 4, MidpointRounding.AwayFromZero);
        if (unitsPerPurchase <= 0) return;

        // The loop starts a day after `start` to align with the cash seeder, whose opening entry occupies the first day.
        // Entries are owned by the guest and not linked to an account, so there is no running value to recalculate.
        List<BondAccountEntry> entries = [];
        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GuestInvestmentPlan.IsBondInvestmentDay(date)) continue;

            entries.Add(new BondAccountEntry(null, 0, DateTime.SpecifyKind(date, DateTimeKind.Utc), 0m, unitsPerPurchase, bondDetails.Id)
            {
                UserId = userId,
                Description = bondDetails.Name
            });
        }

        await bondEntryRepository.Add(entries, false, cancellationToken);
    }
}