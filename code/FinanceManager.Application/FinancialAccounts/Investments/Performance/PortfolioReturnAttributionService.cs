using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// Attributes the selected portfolio value change without overlapping components. Buy and sell
/// rows are the current investment model's external cash-impact representation established by #729;
/// known transaction fees are separated, and the remaining market/valuation effect is calculated as
/// the reconciliation residual after currency translation.
/// </summary>
public class PortfolioReturnAttributionService(
    IFinancialAccountRepository financialAccountRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    IInvestmentPriceProvider investmentPriceProvider,
    BondDashboardContext bondDashboardContext,
    ICurrencyExchangeService currencyExchangeService) : IPortfolioReturnAttributionService
{
    public async Task<PortfolioReturnAttributionResult> GetAsync(
        int userId,
        Currency currency,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        var startDate = start.Date;
        var endDate = (end > DateTime.UtcNow ? DateTime.UtcNow : end).Date;
        if (start == default || end == default || endDate < startDate)
            return PortfolioReturnAttributionResult.InsufficientData(startDate, endDate);

        List<InvestmentAccount> investmentAccounts = [];
        await foreach (var account in financialAccountRepository
                           .GetAccounts<InvestmentAccount>(userId, startDate, endDate)
                           .WithCancellation(cancellationToken))
            investmentAccounts.Add(account);

        List<BondAccount> bondAccounts = [];
        await foreach (var account in financialAccountRepository
                           .GetAccounts<BondAccount>(userId, startDate, endDate)
                           .WithCancellation(cancellationToken))
            bondAccounts.Add(account);

        if (investmentAccounts.Count == 0 && bondAccounts.Count == 0)
            return PortfolioReturnAttributionResult.InsufficientData(startDate, endDate);

        var investmentAccountIds = investmentAccounts.Select(x => x.AccountId).ToArray();
        var transactionFlows = investmentAccountIds.Length == 0
            ? []
            : await investmentTransactionRepository.GetCapitalFlowInputs(
                investmentAccountIds,
                DateOnly.FromDateTime(endDate),
                cancellationToken);

        if (!TryBuildListingCurrencies(transactionFlows, out var listingCurrencies))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(bondAccounts, cancellationToken);
        if (bondAccounts
            .SelectMany(account => account.GetStoredBondsIds())
            .Any(detailsId => !bondDetails.ContainsKey(detailsId)))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var ledger = PortfolioPeriodLedger.Build(transactionFlows, bondAccounts, bondDetails, startDate, endDate);
        var ratesByCurrency = await LoadRatesAsync(
            listingCurrencies.Values
                .Concat(bondDetails.Values.Select(details => NormalizeCurrency(details.Currency.ShortName)))
                .Distinct(StringComparer.OrdinalIgnoreCase),
            currency,
            startDate,
            endDate,
            cancellationToken);

        if (!TryConvertFlows(ledger.Movements, currency, ratesByCurrency, out var externalCashMovement, out var feeEffect))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var openingHoldings = investmentAccountIds.Length == 0
            ? new Dictionary<int, IReadOnlyDictionary<long, decimal>>()
            : await investmentTransactionRepository.GetHoldingsByAccountAsOf(
                investmentAccountIds,
                DateOnly.FromDateTime(startDate).AddDays(-1),
                cancellationToken);
        var endingHoldings = investmentAccountIds.Length == 0
            ? new Dictionary<int, IReadOnlyDictionary<long, decimal>>()
            : await investmentTransactionRepository.GetHoldingsByAccountAsOf(
                investmentAccountIds,
                DateOnly.FromDateTime(endDate),
                cancellationToken);

        var priceCache = new Dictionary<(long ListingId, DateTime Date), decimal>();
        var (openingStockValue, openingStockComplete) = await GetInvestmentValueAsync(
            openingHoldings,
            currency,
            startDate,
            priceCache,
            cancellationToken);
        var (endingStockValue, endingStockComplete) = await GetInvestmentValueAsync(
            endingHoldings,
            currency,
            endDate,
            priceCache,
            cancellationToken);
        if (!openingStockComplete || !endingStockComplete)
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var (investmentFxEffect, investmentFxComplete) = await GetInvestmentFxEffectAsync(
            ledger.QuantityChanges,
            openingHoldings,
            listingCurrencies,
            currency,
            startDate,
            endDate,
            ratesByCurrency,
            priceCache,
            cancellationToken);
        if (!investmentFxComplete)
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var (openingBondValue, openingBondComplete) = GetBondValue(
            bondAccounts,
            bondDetails,
            ratesByCurrency,
            currency,
            DateOnly.FromDateTime(startDate).AddDays(-1),
            DateOnly.FromDateTime(startDate));
        var (endingBondValue, endingBondComplete) = GetBondValue(
            bondAccounts,
            bondDetails,
            ratesByCurrency,
            currency,
            DateOnly.FromDateTime(endDate),
            DateOnly.FromDateTime(endDate));
        if (!openingBondComplete || !endingBondComplete)
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        if (!TryGetBondFxEffect(
                bondAccounts,
                bondDetails,
                currency,
                startDate,
                endDate,
                ratesByCurrency,
                out var bondFxEffect))
            return PortfolioReturnAttributionResult.Unavailable(startDate, endDate);

        var fxEffect = investmentFxEffect + bondFxEffect;
        var totalChange = openingStockValue + openingBondValue;
        var endingValue = endingStockValue + endingBondValue;
        totalChange = endingValue - totalChange;
        var marketEffect = totalChange - externalCashMovement - feeEffect - fxEffect;

        return PortfolioReturnAttributionResult.Available(
            totalChange,
            externalCashMovement,
            marketEffect,
            feeEffect,
            fxEffect,
            startDate,
            endDate);
    }

    private static bool TryBuildListingCurrencies(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactionFlows,
        out Dictionary<long, string> listingCurrencies)
    {
        listingCurrencies = new Dictionary<long, string>();
        foreach (var flow in transactionFlows)
        {
            if (flow.Quantity <= 0m || flow.UnitPrice < 0m || flow.Type is not (InvestmentTransactionType.Buy or InvestmentTransactionType.Sell))
                continue;

            var sourceCurrency = NormalizeCurrency(flow.Currency);
            if (string.IsNullOrWhiteSpace(sourceCurrency))
                return false;

            if (listingCurrencies.TryGetValue(flow.AssetListingId, out var existingCurrency)
                && !string.Equals(existingCurrency, sourceCurrency, StringComparison.OrdinalIgnoreCase))
                return false;

            listingCurrencies[flow.AssetListingId] = sourceCurrency;
        }

        return true;
    }

    private async Task<Dictionary<string, IReadOnlyDictionary<DateTime, decimal>>> LoadRatesAsync(
        IEnumerable<string> sourceCurrencies,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        Dictionary<string, IReadOnlyDictionary<DateTime, decimal>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceCurrency in sourceCurrencies.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var source = new Currency(0, sourceCurrency, sourceCurrency);
            result[sourceCurrency] = await CurrencyRateSeries.LoadAsync(
                currencyExchangeService,
                source,
                targetCurrency,
                startDate,
                endDate,
                cancellationToken);
        }

        return result;
    }

    private static bool TryConvertFlows(
        IEnumerable<PortfolioMovement> movements,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal externalCashMovement,
        out decimal feeEffect)
    {
        externalCashMovement = 0m;
        feeEffect = 0m;
        foreach (var flow in movements.OrderBy(x => x.Date).ThenBy(x => x.SourceId))
        {
            var signedAmount = flow.Kind is PortfolioMovementKind.Sale or PortfolioMovementKind.BondWithdrawal or PortfolioMovementKind.Fee
                ? -flow.Amount : flow.Amount;
            if (!TryConvert(signedAmount, flow.Currency, flow.Date, targetCurrency, ratesByCurrency, out var converted))
                return false;
            if (flow.Kind is PortfolioMovementKind.Fee or PortfolioMovementKind.FeeRebate)
                feeEffect += converted;
            else
                externalCashMovement += converted;
        }

        return true;
    }

    private static (decimal Value, bool Complete) GetBondValue(
        IEnumerable<BondAccount> accounts,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        Currency targetCurrency,
        DateOnly entryAsOfDate,
        DateOnly valuationDate)
    {
        decimal total = 0m;
        foreach (var account in accounts)
        {
            var entries = account.Entries
                .Concat(account.NextOlderEntries.Values)
                .Where(entry => DateOnly.FromDateTime(entry.PostingDate) <= entryAsOfDate)
                .GroupBy(entry => entry.BondDetailsId)
                .Select(group => group.OrderByDescending(entry => entry.PostingDate).ThenByDescending(entry => entry.EntryId).First());

            foreach (var entry in entries)
            {
                if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                    return (0m, false);

                if (!TryGetBondPrice(entry, details, valuationDate, out var value))
                    return (0m, false);
                if (value == 0m && entry.Value != 0m)
                    return (0m, false);

                var sourceCurrency = NormalizeCurrency(details.Currency.ShortName);
                if (!TryConvert(
                        value,
                        sourceCurrency,
                        valuationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        targetCurrency,
                        ratesByCurrency,
                        out var converted))
                    return (0m, false);

                total += converted;
            }
        }

        return (total, true);
    }

    private async Task<(decimal Value, bool Complete)> GetInvestmentValueAsync(
        IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>> holdingsByAccount,
        Currency targetCurrency,
        DateTime date,
        IDictionary<(long ListingId, DateTime Date), decimal> priceCache,
        CancellationToken cancellationToken)
    {
        decimal total = 0m;
        foreach (var holdings in holdingsByAccount.Values)
        {
            foreach (var (listingId, quantity) in holdings)
            {
                if (quantity == 0m) continue;

                var price = await GetCachedPriceAsync(listingId, targetCurrency, date, priceCache, cancellationToken);
                if (price <= 0m)
                    return (0m, false);

                total += quantity * price;
            }
        }

        return (total, true);
    }

    private async Task<(decimal Effect, bool Complete)> GetInvestmentFxEffectAsync(
        IReadOnlyList<PortfolioQuantityChange> quantityChanges,
        IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>> openingHoldings,
        IReadOnlyDictionary<long, string> listingCurrencies,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        IDictionary<(long ListingId, DateTime Date), decimal> priceCache,
        CancellationToken cancellationToken)
    {
        var openingQuantities = new Dictionary<long, decimal>();
        foreach (var holdings in openingHoldings.Values)
        {
            foreach (var (listingId, quantity) in holdings)
            {
                openingQuantities[listingId] = openingQuantities.GetValueOrDefault(listingId) + quantity;
            }
        }

        var flowsByListing = quantityChanges
            .GroupBy(flow => flow.InstrumentId)
            .ToDictionary(group => group.Key, group => group.OrderBy(flow => flow.Date).ThenBy(flow => flow.SourceId).ToList());

        decimal fxEffect = 0m;
        foreach (var listingId in openingQuantities.Keys.Concat(flowsByListing.Keys).Distinct())
        {
            if (!listingCurrencies.TryGetValue(listingId, out var sourceCurrency))
                return (0m, false);
            if (string.Equals(sourceCurrency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
                continue;

            var quantity = openingQuantities.GetValueOrDefault(listingId);
            var currentDate = startDate;
            if (flowsByListing.TryGetValue(listingId, out var flows))
            {
                foreach (var flow in flows)
                {
                    var flowDate = flow.Date;
                    if (flowDate > currentDate && quantity != 0m)
                    {
                        var interval = await GetInvestmentFxIntervalAsync(
                            listingId,
                            quantity,
                            sourceCurrency,
                            targetCurrency,
                            currentDate,
                            flowDate,
                            ratesByCurrency,
                            priceCache,
                            cancellationToken);
                        if (!interval.Complete)
                            return (0m, false);

                        fxEffect += interval.Effect;
                    }

                    quantity += flow.Kind == PortfolioMovementKind.Purchase ? flow.Quantity : -flow.Quantity;
                    currentDate = flowDate;
                }
            }

            if (endDate > currentDate && quantity != 0m)
            {
                var interval = await GetInvestmentFxIntervalAsync(
                    listingId,
                    quantity,
                    sourceCurrency,
                    targetCurrency,
                    currentDate,
                    endDate,
                    ratesByCurrency,
                    priceCache,
                    cancellationToken);
                if (!interval.Complete)
                    return (0m, false);

                fxEffect += interval.Effect;
            }
        }

        return (fxEffect, true);
    }

    private async Task<(decimal Effect, bool Complete)> GetInvestmentFxIntervalAsync(
        long listingId,
        decimal quantity,
        string sourceCurrency,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        IDictionary<(long ListingId, DateTime Date), decimal> priceCache,
        CancellationToken cancellationToken)
    {
        if (!TryGetRatePair(sourceCurrency, targetCurrency, startDate, endDate, ratesByCurrency, out var startRate, out var endRate))
            return (0m, false);

        var endingTargetPrice = await GetCachedPriceAsync(
            listingId,
            targetCurrency,
            endDate,
            priceCache,
            cancellationToken);
        if (endingTargetPrice <= 0m)
            return (0m, false);

        var endingNativePrice = endingTargetPrice / endRate;
        return (quantity * endingNativePrice * (endRate - startRate), true);
    }

    private static bool TryGetBondFxEffect(
        IEnumerable<BondAccount> accounts,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal fxEffect)
    {
        fxEffect = 0m;
        foreach (var account in accounts)
        {
            var entriesByBond = account.Entries
                .Concat(account.NextOlderEntries.Values)
                .GroupBy(entry => entry.BondDetailsId);

            foreach (var bondEntries in entriesByBond)
            {
                if (!bondDetails.TryGetValue(bondEntries.Key, out var details))
                    return false;

                var entries = bondEntries
                    .GroupBy(entry => entry.EntryId)
                    .Select(group => group.First())
                    .OrderBy(entry => entry.PostingDate)
                    .ThenBy(entry => entry.EntryId)
                    .ToList();
                var previousEntry = entries.LastOrDefault(entry => entry.PostingDate.Date < startDate.Date);
                var currentDate = startDate;

                foreach (var entry in entries.Where(entry => entry.PostingDate.Date >= startDate.Date && entry.PostingDate.Date <= endDate.Date))
                {
                    var entryDate = DateTime.SpecifyKind(entry.PostingDate.Date, DateTimeKind.Utc);
                    if (entryDate > currentDate && previousEntry is not null)
                    {
                        if (!TryAddBondFxInterval(
                                previousEntry,
                                details,
                                targetCurrency,
                                currentDate,
                                entryDate,
                                ratesByCurrency,
                                ref fxEffect))
                            return false;
                    }

                    previousEntry = entry;
                    currentDate = entryDate;
                }

                if (previousEntry is not null && endDate > currentDate
                    && !TryAddBondFxInterval(
                        previousEntry,
                        details,
                        targetCurrency,
                        currentDate,
                        endDate,
                        ratesByCurrency,
                        ref fxEffect))
                    return false;
            }
        }

        return true;
    }

    private static bool TryAddBondFxInterval(
        BondAccountEntry entry,
        BondDetails details,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        ref decimal fxEffect)
    {
        var sourceCurrency = NormalizeCurrency(details.Currency.ShortName);
        if (!TryGetRatePair(sourceCurrency, targetCurrency, startDate, endDate, ratesByCurrency, out var startRate, out var endRate))
            return false;

        if (!TryGetBondPrice(entry, details, DateOnly.FromDateTime(endDate), out var endingNativeValue))
            return false;
        if (endingNativeValue == 0m && entry.Value != 0m)
            return false;

        fxEffect += endingNativeValue * (endRate - startRate);
        return true;
    }

    private static bool TryGetBondPrice(
        BondAccountEntry entry,
        BondDetails details,
        DateOnly date,
        out decimal value)
    {
        try
        {
            value = entry.GetPriceAt(date, details);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            value = 0m;
            return false;
        }
    }

    private static bool TryGetRatePair(
        string sourceCurrency,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal startRate,
        out decimal endRate)
    {
        if (string.Equals(sourceCurrency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
        {
            startRate = 1m;
            endRate = 1m;
            return true;
        }

        if (ratesByCurrency.TryGetValue(sourceCurrency, out var rates)
            && CurrencyRateSeries.TryGet(rates, startDate, out startRate)
            && CurrencyRateSeries.TryGet(rates, endDate, out endRate)
            && startRate > 0m
            && endRate > 0m)
            return true;

        startRate = 0m;
        endRate = 0m;
        return false;
    }

    private async Task<decimal> GetCachedPriceAsync(
        long listingId,
        Currency targetCurrency,
        DateTime date,
        IDictionary<(long ListingId, DateTime Date), decimal> priceCache,
        CancellationToken cancellationToken)
    {
        var key = (listingId, date.Date);
        if (priceCache.TryGetValue(key, out var price))
            return price;

        price = await investmentPriceProvider.GetPricePerUnitAsync(
            listingId,
            targetCurrency,
            DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
            cancellationToken);
        priceCache[key] = price;
        return price;
    }

    private static bool TryConvert(
        decimal amount,
        string sourceCurrency,
        DateTime date,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal converted)
    {
        if (amount == 0m || string.Equals(sourceCurrency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
        {
            converted = amount;
            return true;
        }

        if (ratesByCurrency.TryGetValue(sourceCurrency, out var rates)
            && CurrencyRateSeries.TryGet(rates, date, out var rate)
            && rate > 0m)
        {
            converted = amount * rate;
            return true;
        }

        converted = 0m;
        return false;
    }

    private static string NormalizeCurrency(string currency)
    {
        var trimmed = currency.Trim();
        return DefaultCurrency.MinorQuoteUnits.TryGetValue(trimmed, out var majorCurrency)
            ? majorCurrency
            : trimmed;
    }
}