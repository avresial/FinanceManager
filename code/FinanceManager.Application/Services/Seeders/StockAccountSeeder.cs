using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class StockAccountSeeder(
    IFinancialAccountRepository accountRepository,
    IAccountRepository<StockAccount> stockAccountRepository,
    IStockPriceRepository stockPriceRepository,
    ICurrencyRepository currencyRepository,
    ILogger<StockAccountSeeder> logger)
{
    private const string DemoIsin = "CSPX.LON";

    public async Task Seed(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (await stockAccountRepository.GetAvailableAccounts(userId).AnyAsync(cancellationToken)) return;

        logger.LogTrace("Seeding stock account.");
        await SeedStockAccount(userId, start, end, cancellationToken);

        logger.LogTrace("Prefilling stock prices for {Isin}.", DemoIsin);
        await SeedStockPrices(DemoIsin, start, end, cancellationToken);
    }

    private async Task SeedStockAccount(int userId, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var account = new StockAccount(userId, 0, "Stock 1");

        // Track running units held per ticker so a sell never drives the balance below zero.
        decimal heldUnits = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Roughly 70% buy days, 20% sell days, 10% no-op so the seeded history has texture.
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

            account.Add(new StockAccountEntry(account.AccountId, 0, date, 0, change, DemoIsin, InvestmentType.Stock), false);
            heldUnits += change;
        }

        account.RecalculateEntryValues(account.Entries.Count - 1);
        await accountRepository.AddAccount(account);
    }

    private async Task SeedStockPrices(string isin, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var currency = await currencyRepository.GetByCode("USD", cancellationToken)
            ?? await currencyRepository.GetOrAdd("USD", "$", cancellationToken);

        // Random-walk price seed: start near a plausible CSPX.LON value and jitter daily so the guest UI shows life-like valuations.
        decimal price = 400m;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var driftPercent = (decimal)((Random.Shared.NextDouble() - 0.48) * 0.04);
            price = Math.Clamp(price * (1 + driftPercent), 250m, 600m);
            price = Math.Round(price, 2);

            await stockPriceRepository.Add(isin, price, currency, date);
        }
    }
}
