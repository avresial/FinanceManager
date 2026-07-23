using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Application.FinancialAccounts.Investments.Assets;

/// <summary>
/// Surfaces investment accounts (the new asset model) to the dashboard asset aggregates. Replaces the
/// legacy <c>AssetsServiceStock</c>: holdings come from <see cref="InvestmentTransaction"/> rows and are
/// priced through <see cref="IInvestmentValuationService"/> / <see cref="IInvestmentPriceProvider"/>
/// rather than per-ticker <c>StockAccountEntry</c> values.
/// </summary>
internal class AssetsServiceInvestment(
    IFinancialAccountRepository financialAccountRepository,
    IInvestmentValuationService valuationService,
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider,
    ICurrencyExchangeService currencyExchangeService) : IAssetsServiceTyped
{
    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, now.AddDays(-1), now))
        {
            var holdings = await valuationService.GetHoldingsAsOfAsync(account.AccountId, today);
            if (holdings.Count > 0)
                return true;
        }

        return false;
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == default) return [];

        List<int> accountIds = [];
        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, start, end))
            accountIds.Add(account.AccountId);

        Dictionary<DateTime, decimal> values = [];
        var seriesByAccount = await valuationService.GetAccountValueSeriesAsync(accountIds, currency, start, end);
        foreach (var series in seriesByAccount.Values)
        {
            foreach (var (date, value) in series)
            {
                if (!values.TryAdd(date, value))
                    values[date] += value;
            }
        }

        return values.Select(x => new TimeSeriesModel(x.Key, x.Value)).ToList();
    }

    // Investment accounts hold equities/ETFs, classified under the Stock investment type. Other types
    // contribute nothing here so the per-type breakdown does not double count against bonds/cash.
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (investmentType != InvestmentType.Stock) return [];
        return await GetAssetsTimeSeries(userId, currency, start, end);
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        List<InvestmentAccount> accounts = [];
        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, asOfDate.AddMinutes(-1), asOfDate))
            accounts.Add(account);

        var valuesByAccount = await valuationService.GetAccountValueAsync(accounts.Select(a => a.AccountId).ToList(), currency, asOfDate);
        foreach (var account in accounts)
        {
            if (valuesByAccount.TryGetValue(account.AccountId, out var value) && value > 0)
                yield return new NameValueResult(account.Name, value);
        }
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        List<int> accountIds = [];
        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, asOfDate.AddMinutes(-1), asOfDate))
            accountIds.Add(account.AccountId);

        var valuesByAccount = await valuationService.GetAccountValueAsync(accountIds, currency, asOfDate);
        var total = valuesByAccount.Values.Sum();

        if (total > 0)
            yield return new NameValueResult(InvestmentType.Stock.ToString(), total);
    }

    public async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossPerInstrument(int userId, Currency currency, DateTime asOfDate)
    {
        List<UnrealizedGainLossInstrumentResult> results = [];

        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, DateTime.MinValue, asOfDate))
        {
            var transactions = (await transactionRepository.GetByAccount(account.AccountId))
                .Where(t => t.TradeDate <= DateOnly.FromDateTime(asOfDate))
                .ToList();

            foreach (var group in transactions.GroupBy(t => t.AssetListingId))
            {
                var holding = group.Sum(t => t.SignedQuantity);
                if (holding <= 0) continue;

                var listing = group.First().AssetListing;
                var instrumentName = listing?.Ticker ?? group.Key.ToString();

                var buys = group.Where(t => t.Type == InvestmentTransactionType.Buy).ToList();
                var boughtQty = buys.Sum(t => t.Quantity);
                decimal boughtCost = 0m;
                var missingExchangeRate = false;
                foreach (var buy in buys)
                {
                    var exchangeRate = await GetBuyExchangeRateAsync(buy, currency, asOfDate);
                    if (exchangeRate is not decimal rate || rate <= 0m)
                    {
                        missingExchangeRate = true;
                        break;
                    }

                    boughtCost += (buy.Quantity * buy.UnitPrice + (buy.Fee ?? 0m)) * rate;
                }
                var avgCost = boughtQty > 0 ? boughtCost / boughtQty : 0;
                var costBasis = avgCost * holding;

                var price = await priceProvider.GetPricePerUnitAsync(group.Key, currency, asOfDate);
                var currentValue = holding * price;

                var excluded = price <= 0 || missingExchangeRate;
                string? warning = price <= 0
                    ? "No price available for this instrument."
                    : missingExchangeRate ? "No exchange rate available for the transaction date." : null;

                var unrealized = currentValue - costBasis;
                var unrealizedPercent = costBasis == 0 ? 0 : unrealized / costBasis * 100;

                results.Add(new UnrealizedGainLossInstrumentResult(
                    account.AccountId,
                    account.Name,
                    instrumentName,
                    instrumentName,
                    holding,
                    costBasis,
                    currentValue,
                    unrealized,
                    unrealizedPercent,
                    asOfDate,
                    excluded,
                    warning));
            }
        }

        return results;
    }

    public async Task<List<UnrealizedGainLossAccountResult>> GetUnrealizedGainLossPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        var instrumentResults = await GetUnrealizedGainLossPerInstrument(userId, currency, asOfDate);

        List<UnrealizedGainLossAccountResult> results = [];
        foreach (var accountGroup in instrumentResults.GroupBy(x => new { x.AccountId, x.AccountName }))
        {
            var included = accountGroup.Where(x => !x.IsExcludedFromTotals).ToList();
            var excludedCount = accountGroup.Count(x => x.IsExcludedFromTotals);

            var costBasis = included.Sum(x => x.CostBasis);
            var currentValue = included.Sum(x => x.CurrentValue);
            var unrealized = currentValue - costBasis;
            var unrealizedPercent = costBasis == 0 ? 0 : unrealized / costBasis * 100;

            results.Add(new UnrealizedGainLossAccountResult(
                accountGroup.Key.AccountId,
                accountGroup.Key.AccountName,
                costBasis,
                currentValue,
                unrealized,
                unrealizedPercent,
                asOfDate,
                excludedCount));
        }

        return results;
    }

    // The capital value of a holding is its remaining buy cost converted into the target currency at
    // the trade-date rate. Historical FX rates are sometimes missing (old trades, thinly-quoted pairs),
    // and dropping the whole holding from the totals in that case is exactly what made an account's
    // capital value — and therefore its gain/loss — collapse to 0 even while the position was still
    // valued fine. Fall back to the as-of-date rate for the same pair so the position keeps a
    // best-effort capital value instead of being excluded.
    private async Task<decimal?> GetBuyExchangeRateAsync(InvestmentTransaction buy, Currency targetCurrency, DateTime asOfDate)
    {
        if (string.Equals(buy.Currency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var sourceCurrency = new Currency { ShortName = buy.Currency, Symbol = buy.Currency };
        var tradeDateRate = await currencyExchangeService.GetExchangeRateAsync(
            sourceCurrency, targetCurrency, buy.TradeDate.ToDateTime(TimeOnly.MinValue));
        if (tradeDateRate is decimal rate && rate > 0m)
            return rate;

        return await currencyExchangeService.GetExchangeRateAsync(sourceCurrency, targetCurrency, asOfDate);
    }
}