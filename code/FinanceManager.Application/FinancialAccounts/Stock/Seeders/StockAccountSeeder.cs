using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Stock.Seeders;

public class StockAccountSeeder(
    IFinancialAccountRepository accountRepository,
    IAccountRepository<StockAccount> stockAccountRepository,
    IStockPriceRepository stockPriceRepository,
    IStockDetailsRepository stockDetailsRepository,
    ICurrencyRepository currencyRepository,
    ILogger<StockAccountSeeder> logger)
{
    // CSPX.LON is iShares Core S&P 500 UCITS ETF; its real ISIN is IE00B5BMR087.
    // Seeding a matching StockDetails row lets CachingIsinResolver resolve the ticker
    // from the local DB instead of falling through to OpenFIGI on every price lookup.
    private const string _demoTicker = "CSPX.LON";
    private const string _demoIsin = "IE00B5BMR087";

    public async Task Seed(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (await stockAccountRepository.GetAvailableAccounts(userId).AnyAsync(cancellationToken)) return;

        logger.LogTrace("Seeding stock account.");
        var currency = await currencyRepository.GetByCode("USD", cancellationToken)
            ?? await currencyRepository.GetOrAdd("USD", "$", cancellationToken);

        await SeedStockDetails(currency, cancellationToken);

        // Generate the price series before the account so each monthly buy can be sized from that day's price.
        logger.LogTrace("Prefilling stock prices for {Isin}.", _demoIsin);
        var prices = await SeedStockPrices(_demoIsin, currency, start, end, cancellationToken);

        await SeedStockAccount(userId, start, end, prices, cancellationToken);
    }

    private async Task SeedStockDetails(Currency currency, CancellationToken cancellationToken)
    {
        if (await stockDetailsRepository.Get(_demoIsin, cancellationToken) is not null) return;

        await stockDetailsRepository.Add(new StockDetails
        {
            Isin = _demoIsin,
            Ticker = _demoTicker,
            Name = "iShares Core S&P 500 UCITS ETF",
            Type = "ETF",
            Region = "LSE",
            Currency = currency
        }, cancellationToken);
    }

    private async Task SeedStockAccount(int userId, DateTime start, DateTime end, IReadOnlyDictionary<DateTime, decimal> prices, CancellationToken cancellationToken)
    {
        var account = new StockAccount(userId, 0, "Stock 1");

        // Mirror the cash account's recurring "Investment" outflow: on each investment day buy the matching
        // value of shares at that day's price, so the stock holding's cost basis tracks the cash spent and
        // its later growth comes from real price movement rather than arbitrary unit counts. The loop starts
        // a day after `start` to align with the cash seeder, whose opening balance occupies the first day.
        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GuestInvestmentPlan.IsStockInvestmentDay(date)) continue;
            if (!prices.TryGetValue(date.Date, out var price) || price <= 0) continue;

            var units = Math.Round(GuestInvestmentPlan.StockMonthlyAmount / price, 4, MidpointRounding.AwayFromZero);
            if (units <= 0) continue;

            var entry = new StockAccountEntry(account.AccountId, 0, date, 0, units, _demoIsin, InvestmentType.Stock)
            {
                Ticker = _demoTicker
            };
            account.Add(entry, false);
        }

        account.RecalculateEntryValues(account.Entries.Count - 1);
        await accountRepository.AddAccount(account);
    }

    private async Task<Dictionary<DateTime, decimal>> SeedStockPrices(string isin, Currency currency, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        // Random-walk price seed: start near a plausible CSPX.LON value and jitter daily so the guest UI shows life-like valuations.
        var prices = new Dictionary<DateTime, decimal>();
        decimal price = 400m;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var driftPercent = (decimal)((Random.Shared.NextDouble() - 0.48) * 0.04);
            price = Math.Clamp(price * (1 + driftPercent), 250m, 600m);
            price = Math.Round(price, 2);

            prices[date] = price;
            await stockPriceRepository.Add(isin, price, currency, date);
        }

        return prices;
    }
}