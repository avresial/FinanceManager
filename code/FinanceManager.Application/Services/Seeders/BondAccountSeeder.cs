using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class BondAccountSeeder(
    IFinancialAccountRepository accountRepository,
    IAccountRepository<BondAccount> bondAccountRepository,
    IBondDetailsRepository bondDetailsRepository,
    ILogger<BondAccountSeeder> logger)
{
    public async Task Seed(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (await bondAccountRepository.GetAvailableAccounts(userId).AnyAsync(cancellationToken)) return;

        var bondDetails = await bondDetailsRepository.GetAllAsync(cancellationToken).FirstOrDefaultAsync(cancellationToken);
        if (bondDetails is null || bondDetails.Id == 0 || bondDetails.UnitValue <= 0)
        {
            logger.LogWarning("No usable bond details available for guest {UserId}; skipping bond account seeding.", userId);
            return;
        }

        logger.LogTrace("Seeding bond account.");
        await SeedBondAccount(userId, bondDetails, start, end, cancellationToken);
    }

    private async Task SeedBondAccount(int userId, BondDetails bondDetails, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var account = new BondAccount(userId, 0, "Bond 1");

        // Each month buys the fractional number of units the recurring bond investment pays for, so the value
        // of the units acquired matches the cash outflow CurrencyAccountSeeder books on the same day.
        var unitsPerPurchase = Math.Round(GuestInvestmentPlan.BondMonthlyAmount / bondDetails.UnitValue, 4, MidpointRounding.AwayFromZero);
        if (unitsPerPurchase <= 0) return;

        // BondAccount.Add dedups by EntryId, so each seeded entry needs a distinct id to survive. The loop starts
        // a day after `start` to align with the cash seeder, whose opening entries occupy the first day.
        var entryId = 0;
        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GuestInvestmentPlan.IsBondInvestmentDay(date)) continue;

            account.Add(new BondAccountEntry(account.AccountId, entryId++, date, 0, unitsPerPurchase, bondDetails.Id), false);
        }

        account.RecalculateEntryValues(account.Entries.Count - 1);
        await accountRepository.AddAccount(account);
    }
}
