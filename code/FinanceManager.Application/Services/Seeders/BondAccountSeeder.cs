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

        var bondDetailsId = await bondDetailsRepository.GetAllAsync(cancellationToken)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (bondDetailsId == 0)
        {
            logger.LogWarning("No bond details available for guest {UserId}; skipping bond account seeding.", userId);
            return;
        }

        logger.LogTrace("Seeding bond account.");
        await SeedBondAccount(userId, bondDetailsId, start, end);
    }

    private async Task SeedBondAccount(int userId, int bondDetailsId, DateTime start, DateTime end)
    {
        var account = new BondAccount(userId, 0, "Bond 1");

        // Track running units held so a sell never drives the balance below zero.
        decimal heldUnits = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var roll = Random.Shared.Next(0, 100);

            decimal change;
            if (roll < 70 || heldUnits <= 0)
            {
                change = Random.Shared.Next(1, 10);
            }
            else if (roll < 90)
            {
                var maxSell = (int)Math.Min(heldUnits, 8);
                if (maxSell <= 0) continue;
                change = -Random.Shared.Next(1, maxSell + 1);
            }
            else
            {
                continue;
            }

            account.Add(new BondAccountEntry(account.AccountId, 0, date, 0, change, bondDetailsId), false);
            heldUnits += change;
        }

        account.RecalculateEntryValues(account.Entries.Count - 1);
        await accountRepository.AddAccount(account);
    }
}
