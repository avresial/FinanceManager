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
/// Unrealized gain/loss pools every trade of a listing into one weighted-average cost pool (the account-scoped appreciation used by
/// the account details page still reads that account's trades only until the account screens are removed).
/// </summary>
internal class AssetsServiceInvestment(
    IAccountRepository<InvestmentAccount> investmentAccountRepository,
    IInvestmentValuationService valuationService,
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentPriceProvider priceProvider,
    ICurrencyExchangeService currencyExchangeService) : IAssetsServiceTyped, IInvestmentAppreciationService
{
    private const string _investmentsGroupName = "Investments";

    // Legacy parity: any non-zero holding counts, including an oversold (negative) one.
    public async Task<bool> IsAnyAccountWithAssets(int userId) =>
        (await transactionRepository.GetHoldingsByUserAsOf(userId, DateOnly.FromDateTime(DateTime.UtcNow))).Count > 0;

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (start == default) return [];

        var series = await valuationService.GetUserValueSeriesAsync(userId, currency, start, end);
        if (series.Count == 0) return [];

        // The valuation omits days that value to zero; fill them so a position sold out mid-range drops to zero
        // instead of being carried forward by the bucketing, as cash and bonds already do.
        List<TimeSeriesModel> result = [];
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
            result.Add(new TimeSeriesModel(day, series.GetValueOrDefault(day)));
        return result;
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
        var values = await valuationService.GetUserHoldingValuesAsync(userId, currency, asOfDate, cancellationToken);
        if (values.Count == 0) return new([], []);

        return new(GetPerType(values).ToList(), GetPerListing(values).ToList());
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

    private static IEnumerable<NameValueResult> GetPerType(IReadOnlyList<InvestmentHoldingValue> values)
    {
        var total = values.Sum(x => x.Value);
        if (total > 0)
            yield return new NameValueResult(InvestmentType.Stock.ToString(), total);
    }

    // One row per held (positive) listing, named by ticker. Two shown listings that share a ticker also show their exchange so the rows stay distinguishable.
    private static IEnumerable<NameValueResult> GetPerListing(IReadOnlyList<InvestmentHoldingValue> values)
    {
        var shown = values.Where(x => x.Value > 0).ToList();
        var tickerCounts = shown.GroupBy(x => x.Holding.Ticker).ToDictionary(g => g.Key, g => g.Count());

        foreach (var (holding, value) in shown)
        {
            var name = tickerCounts[holding.Ticker] > 1 && !string.IsNullOrWhiteSpace(holding.ExchangeName)
                ? $"{holding.Ticker} ({holding.ExchangeName})"
                : holding.Ticker;
            yield return new NameValueResult(name, value) { Kind = InvestmentType.Stock.ToString() };
        }
    }

    public async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossPerInstrument(int userId, Currency currency, DateTime asOfDate)
    {
        if (userId == 0) return [];

        // A trade posted at any time on the as-of day counts, so read through the following midnight and keep the day filter below.
        var transactions = await transactionRepository.GetByUser(userId, DateOnly.MinValue, asOfDate.Date == DateTime.MaxValue.Date ? DateOnly.MaxValue : DateOnly.FromDateTime(asOfDate).AddDays(1));
        return await GetUnrealizedGainLossInstrumentsAsync(transactions, null, _investmentsGroupName, currency, asOfDate);
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
            await transactionRepository.GetByAccount(selectedAccount.AccountId, cancellationToken),
            selectedAccount.AccountId,
            selectedAccount.Name,
            currency,
            asOfDate,
            cancellationToken);

        if (instruments.Count == 0) return null;

        return AggregateAccountResult(selectedAccount.AccountId, selectedAccount.Name, instruments, asOfDate);
    }

    public async Task<List<UnrealizedGainLossAccountResult>> GetUnrealizedGainLossPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        var instrumentResults = await GetUnrealizedGainLossPerInstrument(userId, currency, asOfDate);

        // Pooled holdings belong to the owner, not to an account: one aggregate row without an account id.
        return instrumentResults.Count == 0
            ? []
            : [AggregateAccountResult(null, _investmentsGroupName, instrumentResults, asOfDate)];
    }

    private async Task<List<UnrealizedGainLossInstrumentResult>> GetUnrealizedGainLossInstrumentsAsync(
        IReadOnlyList<InvestmentTransaction> allTransactions,
        int? accountId,
        string accountName,
        Currency currency,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var transactions = allTransactions
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
                accountId,
                accountName,
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
        int? accountId,
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