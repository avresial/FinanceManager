using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Application.FinancialAccounts.Investments.Assets;

/// <summary>
/// Surfaces the user's pooled investment holdings (the new asset model) to the dashboard asset aggregates, and provides
/// account-scoped appreciation for account-detail views. Replaces the legacy <c>AssetsServiceStock</c>:
/// holdings come from <see cref="InvestmentTransaction"/> rows pooled per listing across the owner's trades, whether or not a trade is
/// linked to an account, and are priced through <see cref="IInvestmentValuationService"/> / <see cref="IInvestmentPriceProvider"/>.
/// Unrealized gain/loss still reads account trades until the portfolio step.
/// </summary>
internal class AssetsServiceInvestment(
    IFinancialAccountRepository financialAccountRepository,
    IAccountRepository<InvestmentAccount> investmentAccountRepository,
    IInvestmentValuationService valuationService,
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider,
    ICurrencyExchangeService currencyExchangeService) : IAssetsServiceTyped, IInvestmentAppreciationService
{
    // Legacy parity: any non-zero holding counts, including an oversold (negative) one.
    public async Task<bool> IsAnyAccountWithAssets(int userId) =>
        (await transactionRepository.GetHoldingsByUserAsOf(userId, DateOnly.FromDateTime(DateTime.UtcNow))).Count > 0;

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == default) return [];

        var series = await valuationService.GetUserValueSeriesAsync(userId, currency, start, end);
        return [.. series.Select(x => new TimeSeriesModel(x.Key, x.Value))];
    }

    // Investments hold equities/ETFs, classified under the Stock investment type. Other types
    // contribute nothing here so the per-type breakdown does not double count against bonds/cash.
    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        if (investmentType != InvestmentType.Stock) return [];
        return await GetAssetsTimeSeries(userId, currency, start, end);
    }

    public async Task<AssetsDistributionResult> GetEndAssetsDistribution(
        int userId, Currency currency, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var values = await valuationService.GetUserValueByListingAsync(userId, currency, asOfDate, cancellationToken);
        if (values.Count == 0) return new([], []);

        var holdings = await transactionRepository.GetHoldingsByUserAsOf(userId, DateOnly.FromDateTime(asOfDate), cancellationToken);
        return new(GetPerType(values).ToList(), GetPerListing(values, holdings).ToList());
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        foreach (var row in (await GetEndAssetsDistribution(userId, currency, asOfDate)).PerAccount)
            yield return row;
    }

    public async IAsyncEnumerable<NameValueResult> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        foreach (var row in (await GetEndAssetsDistribution(userId, currency, asOfDate)).PerType)
            yield return row;
    }

    private static IEnumerable<NameValueResult> GetPerType(IReadOnlyDictionary<long, decimal> values)
    {
        var total = values.Values.Sum();
        if (total > 0)
            yield return new NameValueResult(InvestmentType.Stock.ToString(), total);
    }

    // One row per held listing, named by ticker. Two held listings that share a ticker also show their exchange so the rows stay distinguishable.
    private static IEnumerable<NameValueResult> GetPerListing(IReadOnlyDictionary<long, decimal> values, IReadOnlyList<InvestmentHolding> holdings)
    {
        var listings = holdings.ToDictionary(h => h.AssetListingId);
        var tickerCounts = values.Keys
            .Select(id => listings.TryGetValue(id, out var holding) ? holding.Ticker : null)
            .Where(ticker => ticker is not null)
            .GroupBy(ticker => ticker!)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (listingId, value) in values.Where(x => x.Value > 0))
        {
            var name = $"Listing {listingId}";
            if (listings.TryGetValue(listingId, out var holding))
                name = tickerCounts[holding.Ticker] > 1 ? $"{holding.Ticker} ({holding.ExchangeName})" : holding.Ticker;

            yield return new NameValueResult(name, value);
        }
    }

    public async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossPerInstrument(int userId, Currency currency, DateTime asOfDate)
    {
        List<UnrealizedGainLossInstrumentResult> results = [];

        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, DateTime.MinValue, asOfDate))
            results.AddRange(await GetUnrealizedGainLossInstrumentsAsync(account, currency, asOfDate));

        return results;
    }

    public async Task<UnrealizedGainLossAccountResult?> GetForAccountAsync(
        int userId,
        int accountId,
        Currency currency,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var selectedAccount = await investmentAccountRepository.Get(accountId, cancellationToken);

        // The direct lookup avoids materialising the user's full portfolio. Returning null for a
        // missing account or another user's account prevents this application boundary from leaking
        // account existence.
        if (selectedAccount is null || selectedAccount.UserId != userId) return null;

        var instruments = await GetUnrealizedGainLossInstrumentsAsync(
            selectedAccount,
            currency,
            asOfDate,
            cancellationToken);

        if (instruments.Count == 0) return null;

        return AggregateAccountResult(selectedAccount.AccountId, selectedAccount.Name, instruments, asOfDate);
    }

    public async Task<List<UnrealizedGainLossAccountResult>> GetUnrealizedGainLossPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        var instrumentResults = await GetUnrealizedGainLossPerInstrument(userId, currency, asOfDate);

        List<UnrealizedGainLossAccountResult> results = [];
        foreach (var accountGroup in instrumentResults.GroupBy(x => new { x.AccountId, x.AccountName }))
            results.Add(AggregateAccountResult(accountGroup.Key.AccountId, accountGroup.Key.AccountName, accountGroup, asOfDate));

        return results;
    }

    private async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossInstrumentsAsync(
        InvestmentAccount account,
        Currency currency,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var transactions = (await transactionRepository.GetByAccount(account.AccountId, cancellationToken))
            .Where(t => t.TradeDate <= DateOnly.FromDateTime(asOfDate))
            .ToList();

        List<UnrealizedGainLossInstrumentResult> results = [];
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
                var exchangeRate = await GetBuyExchangeRateAsync(buy, currency, asOfDate, cancellationToken);
                if (exchangeRate is not decimal rate || rate <= 0m)
                {
                    missingExchangeRate = true;
                    break;
                }

                boughtCost += (buy.Quantity * buy.UnitPrice + (buy.Fee ?? 0m)) * rate;
            }

            var avgCost = boughtQty > 0 ? boughtCost / boughtQty : 0m;
            var costBasis = avgCost * holding;
            var price = await priceProvider.GetPricePerUnitAsync(group.Key, currency, asOfDate, cancellationToken);
            var currentValue = holding * price;
            var excluded = price <= 0 || missingExchangeRate;
            string? warning = price <= 0
                ? "No price available for this instrument."
                : missingExchangeRate ? "No exchange rate available for the transaction date." : null;
            var gainLoss = new GainLoss(costBasis, currentValue);

            results.Add(new UnrealizedGainLossInstrumentResult(
                account.AccountId,
                account.Name,
                instrumentName,
                instrumentName,
                holding,
                costBasis,
                currentValue,
                gainLoss.Amount,
                gainLoss.Percent,
                asOfDate,
                excluded,
                warning));
        }

        return results;
    }

    private static UnrealizedGainLossAccountResult AggregateAccountResult(
        int accountId,
        string accountName,
        IEnumerable<UnrealizedGainLossInstrumentResult> instrumentResults,
        DateTime asOfDate)
    {
        var instruments = instrumentResults.ToList();
        var included = instruments.Where(x => !x.IsExcludedFromTotals).ToList();
        var costBasis = included.Sum(x => x.CostBasis);
        var currentValue = included.Sum(x => x.CurrentValue);
        var gainLoss = new GainLoss(costBasis, currentValue);

        return new UnrealizedGainLossAccountResult(
            accountId,
            accountName,
            costBasis,
            currentValue,
            gainLoss.Amount,
            gainLoss.Percent,
            asOfDate,
            instruments.Count(x => x.IsExcludedFromTotals));
    }

    // The capital value of a holding is its remaining buy cost converted into the target currency at
    // the trade-date rate. Historical FX rates are sometimes missing (old trades, thinly-quoted pairs),
    // and dropping the whole holding from the totals in that case is exactly what made an account's
    // capital value — and therefore its gain/loss — collapse to 0 even while the position was still
    // valued fine. Fall back to the as-of-date rate for the same pair so the position keeps a
    // best-effort capital value instead of being excluded.
    private async Task<decimal?> GetBuyExchangeRateAsync(
        InvestmentTransaction buy,
        Currency targetCurrency,
        DateTime asOfDate,
        CancellationToken cancellationToken)
    {
        var purchaseCurrency = buy.Currency;
        if (string.Equals(purchaseCurrency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var sourceCurrency = new Currency { ShortName = purchaseCurrency, Symbol = purchaseCurrency };
        var tradeDateRate = cancellationToken.CanBeCanceled
            ? await currencyExchangeService.GetExchangeRateAsync(
                sourceCurrency,
                targetCurrency,
                buy.TradeDate.ToDateTime(TimeOnly.MinValue),
                cancellationToken)
            : await currencyExchangeService.GetExchangeRateAsync(
                sourceCurrency,
                targetCurrency,
                buy.TradeDate.ToDateTime(TimeOnly.MinValue));
        if (tradeDateRate is decimal rate && rate > 0m)
            return rate;

        return cancellationToken.CanBeCanceled
            ? await currencyExchangeService.GetExchangeRateAsync(sourceCurrency, targetCurrency, asOfDate, cancellationToken)
            : await currencyExchangeService.GetExchangeRateAsync(sourceCurrency, targetCurrency, asOfDate);
    }
}