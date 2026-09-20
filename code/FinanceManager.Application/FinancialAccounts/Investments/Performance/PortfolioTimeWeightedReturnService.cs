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
/// Builds daily portfolio valuation periods for the time-weighted return calculation. The current
/// investment model represents external investment cash impact with Buy/Sell rows (the semantics
/// established by #729); bond ValueChange rows provide the equivalent unit movement.
/// </summary>
public class PortfolioTimeWeightedReturnService(
    IFinancialAccountRepository financialAccountRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    IInvestmentPriceProvider investmentPriceProvider,
    BondDashboardContext bondDashboardContext,
    ICurrencyExchangeService currencyExchangeService) : ITimeWeightedReturnService
{
    public async Task<TimeWeightedReturnResult> GetAsync(
        int userId,
        Currency currency,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        var startDate = start.Date;
        var endDate = (end > DateTime.UtcNow ? DateTime.UtcNow : end).Date;
        if (start == default || end == default || endDate < startDate)
            return Result(null, TimeWeightedReturnStatus.InsufficientData, startDate, endDate);

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
            return Result(null, TimeWeightedReturnStatus.InsufficientData, startDate, endDate);

        var investmentAccountIds = investmentAccounts.Select(x => x.AccountId).ToArray();
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactionFlows = investmentAccountIds.Length == 0
            ? []
            : await investmentTransactionRepository.GetCapitalFlowInputs(
                investmentAccountIds,
                DateOnly.FromDateTime(endDate),
                cancellationToken);
        var valuationInputs = investmentAccountIds.Length == 0
            ? new IInvestmentTransactionRepository.AccountValuationInputs([], [])
            : await investmentTransactionRepository.GetValuationInputs(
                investmentAccountIds,
                DateOnly.FromDateTime(startDate),
                DateOnly.FromDateTime(endDate),
                cancellationToken);

        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(bondAccounts, cancellationToken);
        if (bondAccounts
            .SelectMany(account => account.GetStoredBondsIds())
            .Any(detailsId => !bondDetails.ContainsKey(detailsId)))
            return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);

        var pendingFlows = BuildPendingFlows(transactionFlows, bondAccounts, bondDetails, startDate, endDate);
        var ratesByCurrency = await LoadRatesAsync(
            pendingFlows,
            bondDetails.Values,
            currency,
            startDate,
            endDate,
            cancellationToken);
        if (!TryConvertFlows(pendingFlows, currency, ratesByCurrency, out var externalFlowsByDate))
            return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);

        try
        {
            var investmentValues = await BuildInvestmentValuesAsync(
                valuationInputs,
                currency,
                startDate,
                endDate,
                cancellationToken);
            if (!investmentValues.Complete)
                return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);

            var bondValues = BuildBondValues(
                bondAccounts,
                bondDetails,
                ratesByCurrency,
                currency,
                startDate,
                endDate);
            if (!bondValues.Complete)
                return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);

            var periods = BuildPeriods(
                investmentValues,
                bondValues,
                externalFlowsByDate,
                startDate,
                endDate);
            return TimeWeightedReturnCalculator.Calculate(periods, startDate, endDate);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            return Result(null, TimeWeightedReturnStatus.Unavailable, startDate, endDate);
        }
    }

    private async Task<ValuationSeries> BuildInvestmentValuesAsync(
        IInvestmentTransactionRepository.AccountValuationInputs inputs,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var openingHoldings = inputs.OpeningPositions
            .GroupBy(position => (position.AccountId, position.AssetListingId))
            .ToDictionary(group => group.Key, group => group.Sum(position => position.Quantity));
        var tradesByDate = inputs.InWindowTrades
            .GroupBy(trade => trade.TradeDate)
            .ToDictionary(
                group => group.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                group => group
                    .GroupBy(trade => (trade.AccountId, trade.AssetListingId))
                    .ToDictionary(tradeGroup => tradeGroup.Key, tradeGroup => tradeGroup.Sum(trade => trade.SignedQuantity)));

        var listingIds = openingHoldings.Keys
            .Select(key => key.AssetListingId)
            .Concat(tradesByDate.Values.SelectMany(day => day.Keys).Select(key => key.AssetListingId))
            .Distinct()
            .ToList();
        var priceSeries = new Dictionary<long, IReadOnlyDictionary<DateTime, decimal>>();
        foreach (var listingId in listingIds)
        {
            priceSeries[listingId] = await investmentPriceProvider.GetPricePerUnitSeriesAsync(
                listingId,
                targetCurrency,
                startDate,
                endDate,
                cancellationToken);
        }

        decimal openingValue = 0m;
        foreach (var (key, quantity) in openingHoldings)
        {
            if (quantity == 0m) continue;
            if (!TryGetPrice(priceSeries, key.AssetListingId, startDate, out var price))
                return new(new Dictionary<DateTime, decimal>(), 0m, false);

            openingValue += quantity * price;
        }

        var holdings = openingHoldings.ToDictionary();
        Dictionary<DateTime, decimal> values = [];
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (tradesByDate.TryGetValue(date, out var dailyTrades))
            {
                foreach (var (key, quantity) in dailyTrades)
                    holdings[key] = holdings.GetValueOrDefault(key) + quantity;
            }

            decimal value = 0m;
            foreach (var (key, quantity) in holdings)
            {
                if (quantity == 0m) continue;
                if (!TryGetPrice(priceSeries, key.AssetListingId, date, out var price))
                    return new(new Dictionary<DateTime, decimal>(), 0m, false);

                value += quantity * price;
            }

            values[date] = value;
        }

        return new(values, openingValue, true);
    }

    private static ValuationSeries BuildBondValues(
        IReadOnlyList<BondAccount> accounts,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate)
    {
        var entriesByAccount = accounts
            .Select(account => account.Entries
                .Concat(account.NextOlderEntries.Values)
                .GroupBy(entry => entry.BondDetailsId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<BondAccountEntry>)group
                        .OrderByDescending(entry => entry.PostingDate)
                        .ThenByDescending(entry => entry.EntryId)
                        .ToList()))
            .ToList();

        decimal openingValue = 0m;
        foreach (var entries in entriesByAccount)
        {
            var result = GetBondValueAt(
                entries,
                bondDetails,
                ratesByCurrency,
                targetCurrency,
                DateOnly.FromDateTime(startDate),
                includeDate: false);
            if (!result.Complete)
                return new(new Dictionary<DateTime, decimal>(), 0m, false);

            openingValue += result.Value;
        }

        Dictionary<DateTime, decimal> values = [];
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            decimal value = 0m;
            foreach (var entries in entriesByAccount)
            {
                var result = GetBondValueAt(
                    entries,
                    bondDetails,
                    ratesByCurrency,
                    targetCurrency,
                    DateOnly.FromDateTime(date),
                    includeDate: true);
                if (!result.Complete)
                    return new(new Dictionary<DateTime, decimal>(), 0m, false);

                value += result.Value;
            }

            values[date] = value;
        }

        return new(values, openingValue, true);
    }

    private static (decimal Value, bool Complete) GetBondValueAt(
        IReadOnlyDictionary<int, IReadOnlyList<BondAccountEntry>> entriesByBond,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        Currency targetCurrency,
        DateOnly valuationDate,
        bool includeDate)
    {
        decimal total = 0m;
        foreach (var entries in entriesByBond.Values)
        {
            var entry = entries
                .Where(candidate =>
                {
                    var postingDate = DateOnly.FromDateTime(candidate.PostingDate);
                    return includeDate ? postingDate <= valuationDate : postingDate < valuationDate;
                })
                .FirstOrDefault();
            if (entry is null || entry.Value == 0m) continue;

            if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                return (0m, false);

            var value = entry.GetPriceAt(valuationDate, details);
            if (value <= 0m)
                return (0m, false);

            if (!TryConvert(
                    value,
                    details.Currency.ShortName,
                    valuationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    targetCurrency,
                    ratesByCurrency,
                    out var converted))
                return (0m, false);

            total += converted;
        }

        return (total, true);
    }

    private static List<TimeWeightedReturnPeriod> BuildPeriods(
        ValuationSeries investmentValues,
        ValuationSeries bondValues,
        IReadOnlyDictionary<DateTime, decimal> externalFlowsByDate,
        DateTime startDate,
        DateTime endDate)
    {
        List<TimeWeightedReturnPeriod> periods = [];
        var startingValue = investmentValues.OpeningValue + bondValues.OpeningValue;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var endingValue = investmentValues.Values.GetValueOrDefault(date)
                              + bondValues.Values.GetValueOrDefault(date);
            var externalFlow = externalFlowsByDate.GetValueOrDefault(date);
            if (startingValue == 0m && externalFlow == 0m && endingValue == 0m)
            {
                startingValue = endingValue;
                continue;
            }

            periods.Add(new(date, startingValue, externalFlow, endingValue));
            startingValue = endingValue;
        }

        return periods;
    }

    private static List<PendingFlow> BuildPendingFlows(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactionFlows,
        IReadOnlyList<BondAccount> bondAccounts,
        IReadOnlyDictionary<int, BondDetails> bondDetails,
        DateTime startDate,
        DateTime endDate)
    {
        List<PendingFlow> result = [];

        foreach (var flow in transactionFlows)
        {
            var date = flow.TradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            if (date < startDate
                || date > endDate
                || flow.Quantity <= 0m
                || flow.UnitPrice < 0m
                || flow.Type is not (InvestmentTransactionType.Buy or InvestmentTransactionType.Sell))
                continue;

            var sourceCurrency = flow.Currency.Trim();
            var isMinorQuote = DefaultCurrency.MinorQuoteUnits.TryGetValue(sourceCurrency, out var majorCurrency);
            var multiplier = isMinorQuote ? flow.ListingPriceMultiplier ?? 0.01m : 1m;
            var amount = flow.Quantity * flow.UnitPrice * multiplier;
            var fee = flow.Fee ?? 0m;
            amount = flow.Type == InvestmentTransactionType.Buy ? amount + fee : amount - fee;
            if (amount == 0m) continue;

            result.Add(new PendingFlow(
                date,
                flow.Type == InvestmentTransactionType.Buy ? amount : -amount,
                isMinorQuote ? majorCurrency! : sourceCurrency,
                flow.TransactionId));
        }

        foreach (var account in bondAccounts)
        {
            foreach (var entry in account.Entries)
            {
                var date = entry.PostingDate.Date;
                if (date < startDate || date > endDate || entry.ValueChange == 0m)
                    continue;
                if (!bondDetails.TryGetValue(entry.BondDetailsId, out var details))
                    throw new InvalidOperationException($"Bond valuation requires details for bond id {entry.BondDetailsId}.");

                result.Add(new PendingFlow(
                    DateTime.SpecifyKind(date, DateTimeKind.Utc),
                    entry.ValueChange * details.UnitValue,
                    details.Currency.ShortName,
                    entry.EntryId));
            }
        }

        return result;
    }

    private async Task<Dictionary<string, IReadOnlyDictionary<DateTime, decimal>>> LoadRatesAsync(
        IReadOnlyCollection<PendingFlow> pendingFlows,
        IEnumerable<BondDetails> bondDetails,
        Currency targetCurrency,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var currencies = pendingFlows
            .Select(flow => flow.Currency)
            .Concat(bondDetails.Select(details => details.Currency.ShortName))
            .Where(currency => !string.IsNullOrWhiteSpace(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, IReadOnlyDictionary<DateTime, decimal>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceCurrency in currencies)
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
        IEnumerable<PendingFlow> pendingFlows,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out Dictionary<DateTime, decimal> externalFlowsByDate)
    {
        externalFlowsByDate = [];
        foreach (var flow in pendingFlows.OrderBy(x => x.Date).ThenBy(x => x.Sequence))
        {
            if (!TryConvert(flow.Amount, flow.Currency, flow.Date, targetCurrency, ratesByCurrency, out var amount))
            {
                externalFlowsByDate.Clear();
                return false;
            }

            externalFlowsByDate[flow.Date.Date] = externalFlowsByDate.GetValueOrDefault(flow.Date.Date) + amount;
        }

        return true;
    }

    private static bool TryGetPrice(
        IReadOnlyDictionary<long, IReadOnlyDictionary<DateTime, decimal>> priceSeries,
        long listingId,
        DateTime date,
        out decimal price)
    {
        price = 0m;
        return priceSeries.TryGetValue(listingId, out var listingPrices)
               && listingPrices.TryGetValue(date.Date, out price)
               && price > 0m;
    }

    private static bool TryConvert(
        decimal amount,
        string sourceCurrency,
        DateTime date,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        out decimal converted)
    {
        if (string.Equals(sourceCurrency.Trim(), targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
        {
            converted = amount;
            return true;
        }

        if (ratesByCurrency.TryGetValue(sourceCurrency.Trim(), out var rates)
            && CurrencyRateSeries.TryGet(rates, date, out var rate)
            && rate > 0m)
        {
            converted = amount * rate;
            return true;
        }

        converted = 0m;
        return false;
    }

    private static TimeWeightedReturnResult Result(
        decimal? totalReturn,
        TimeWeightedReturnStatus status,
        DateTime startDate,
        DateTime endDate) =>
        new(totalReturn, status, startDate, endDate);

    private sealed record PendingFlow(DateTime Date, decimal Amount, string Currency, long Sequence);

    private sealed record ValuationSeries(
        IReadOnlyDictionary<DateTime, decimal> Values,
        decimal OpeningValue,
        bool Complete);
}