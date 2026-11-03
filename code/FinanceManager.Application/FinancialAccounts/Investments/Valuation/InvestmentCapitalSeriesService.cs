using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;

namespace FinanceManager.Application.FinancialAccounts.Investments.Valuation;

/// <summary>
/// Replays an investment account's Buy/Sell rows into a daily weighted-average cost-basis series,
/// converting each purchase to the target currency at its trade-date rate.
/// </summary>
internal class InvestmentCapitalSeriesService(
    IInvestmentTransactionRepository transactionRepository,
    ICurrencyExchangeService currencyExchangeService) : IInvestmentCapitalSeriesService
{
    public async Task<IReadOnlyDictionary<int, IReadOnlyDictionary<DateTime, decimal>>> GetCapitalSeriesAsync(
        IReadOnlyCollection<int> accountIds,
        Currency targetCurrency,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        var result = new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>();
        if (accountIds.Count == 0 || start == default || end == default || end < start)
            return result;

        var endDate = end.Date;
        var endDateOnly = DateOnly.FromDateTime(endDate);

        var capitalFlowInputs = await transactionRepository.GetCapitalFlowInputs(accountIds, endDateOnly, ct);
        var capitalFlows = capitalFlowInputs
            .Select(ToCapitalFlow)
            .ToList();
        if (capitalFlows.Count == 0) return result;

        var buyFlows = capitalFlows
            .Where(flow => flow.Type == InvestmentTransactionType.Buy)
            .ToList();
        if (buyFlows.Count == 0) return result;

        var ratesByCurrency = await LoadCapitalRatesAsync(buyFlows, targetCurrency, endDate, ct);
        foreach (var accountGroup in capitalFlows.GroupBy(flow => flow.AccountId))
        {
            if (!accountGroup.Any(flow => flow.Type == InvestmentTransactionType.Buy)) continue;

            var series = BuildCapitalSeries(accountGroup, targetCurrency, ratesByCurrency, start.Date, endDate);
            if (series.Count > 0)
                result[accountGroup.Key] = series;
        }

        return result;
    }

    private async Task<Dictionary<string, IReadOnlyDictionary<DateTime, decimal>>> LoadCapitalRatesAsync(
        IReadOnlyCollection<InvestmentCapitalFlow> flows,
        Currency targetCurrency,
        DateTime endDate,
        CancellationToken ct)
    {
        Dictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (var currencyGroup in flows.GroupBy(flow => flow.Currency, StringComparer.OrdinalIgnoreCase))
        {
            var firstDate = currencyGroup.Min(flow => flow.Date);
            var sourceCurrency = new Currency(0, currencyGroup.Key, currencyGroup.Key);
            ratesByCurrency[currencyGroup.Key] = await CurrencyRateSeries.LoadAsync(
                currencyExchangeService, sourceCurrency, targetCurrency, firstDate, endDate);
        }

        return ratesByCurrency;
    }

    private static Dictionary<DateTime, decimal> BuildCapitalSeries(
        IEnumerable<InvestmentCapitalFlow> accountFlows,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency,
        DateTime startDate,
        DateTime endDate)
    {
        Dictionary<DateTime, decimal> dailyDeltas = [];
        decimal openingCapital = 0m;
        Dictionary<long, InvestmentPositionCost> positions = [];

        foreach (var flow in accountFlows.OrderBy(flow => flow.Date).ThenBy(flow => flow.TransactionId))
        {
            var before = GetTotalCostBasis(positions);
            switch (flow.Type)
            {
                case InvestmentTransactionType.Buy:
                    {
                        if (flow.Quantity <= 0m) break;

                        var convertedCost = ConvertBuyCost(flow, targetCurrency, ratesByCurrency);
                        if (convertedCost is not decimal cost) break;

                        if (!positions.TryGetValue(flow.AssetListingId, out var position))
                        {
                            position = new InvestmentPositionCost();
                            positions[flow.AssetListingId] = position;
                        }

                        position.Quantity += flow.Quantity;
                        position.CostBasis += cost;
                        break;
                    }
                case InvestmentTransactionType.Sell:
                    RemoveCostBasis(positions, flow.AssetListingId, flow.Quantity);
                    break;
            }

            var after = GetTotalCostBasis(positions);
            var delta = after - before;

            if (flow.Date < startDate)
                openingCapital = after;
            else if (flow.Date <= endDate)
                dailyDeltas[flow.Date] = dailyDeltas.GetValueOrDefault(flow.Date) + delta;
        }

        Dictionary<DateTime, decimal> result = [];
        var capital = openingCapital;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            capital += dailyDeltas.GetValueOrDefault(date);
            result[date] = capital;
        }

        return result;
    }

    private static decimal GetTotalCostBasis(IReadOnlyDictionary<long, InvestmentPositionCost> positions) =>
        positions.Values.Sum(position => position.CostBasis);

    private static void RemoveCostBasis(
        IDictionary<long, InvestmentPositionCost> positions,
        long assetListingId,
        decimal quantity)
    {
        if (quantity <= 0m || !positions.TryGetValue(assetListingId, out var position) || position.Quantity <= 0m)
            return;

        var soldQuantity = Math.Min(quantity, position.Quantity);
        if (soldQuantity == position.Quantity)
        {
            positions.Remove(assetListingId);
            return;
        }

        position.CostBasis -= position.CostBasis * soldQuantity / position.Quantity;
        position.Quantity -= soldQuantity;
    }

    private static decimal? ConvertBuyCost(
        InvestmentCapitalFlow flow,
        Currency targetCurrency,
        IReadOnlyDictionary<string, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrency)
    {
        var amount = (flow.Quantity * flow.UnitPrice + (flow.Fee ?? 0m)) * flow.PriceMultiplier;
        if (string.Equals(flow.Currency, targetCurrency.ShortName, StringComparison.OrdinalIgnoreCase))
            return amount;

        return ratesByCurrency.TryGetValue(flow.Currency, out var rates)
            && CurrencyRateSeries.TryGet(rates, flow.Date, out var rate)
            && rate > 0m
                ? amount * rate
                : null;
    }

    private static InvestmentCapitalFlow ToCapitalFlow(IInvestmentTransactionRepository.CapitalFlowInput flow)
    {
        var currency = flow.Currency.Trim();
        var isMinorQuote = DefaultCurrency.MinorQuoteUnits.TryGetValue(currency, out var majorCurrency);
        var multiplier = isMinorQuote ? 0.01m : 1m;

        return new InvestmentCapitalFlow(
            flow.TransactionId,
            flow.AccountId,
            flow.AssetListingId,
            flow.TradeDate.ToDateTime(TimeOnly.MinValue).Date,
            flow.Type,
            flow.Quantity,
            flow.UnitPrice,
            flow.Fee,
            isMinorQuote ? majorCurrency! : currency,
            multiplier);
    }

    private sealed record InvestmentCapitalFlow(
        long TransactionId,
        int AccountId,
        long AssetListingId,
        DateTime Date,
        InvestmentTransactionType Type,
        decimal Quantity,
        decimal UnitPrice,
        decimal? Fee,
        string Currency,
        decimal PriceMultiplier);

    private sealed class InvestmentPositionCost
    {
        public decimal Quantity { get; set; }
        public decimal CostBasis { get; set; }
    }
}