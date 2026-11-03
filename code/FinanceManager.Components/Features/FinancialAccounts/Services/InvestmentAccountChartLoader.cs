using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

/// <summary>
/// Fetches and assembles everything the investment account chart shows for one range: value,
/// benchmark and capital series, holdings, and the capital/current-value appreciation figures.
/// Holds no component state, so the page decides when to load and how to paint the result.
/// </summary>
public sealed class InvestmentAccountChartLoader(
    InvestmentTransactionHttpClient transactionHttpClient,
    InvestmentValuationHttpClient valuationHttpClient,
    AssetsHttpClient assetsHttpClient,
    MoneyFlowHttpClient moneyFlowHttpClient)
{
    public async Task<InvestmentAccountChartModel> LoadAsync(
        int userId,
        int accountId,
        Currency currency,
        string selectedRange,
        DateTime dateStart,
        DateTime dateEnd,
        InstrumentSearchResultDto? benchmark,
        string benchmarkName)
    {
        // These requests use separate API scopes and have no data dependency on one another. Keep
        // them on the same critical path only at the join so one slow valuation cannot serialize the
        // other chart inputs or delay the first render unnecessarily.
        var holdingMetadataTask = transactionHttpClient.GetHoldingMetadataAsync(accountId, dateEnd);
        var chartRequests = await InvestmentChartRequestLoader.LoadAsync(
            () => valuationHttpClient.GetValueSeriesAsync(accountId, currency.Id, dateStart, dateEnd),
            () => valuationHttpClient.GetHoldingsAsync(accountId, dateEnd),
            async () =>
            {
                var accountAppreciation = await assetsHttpClient.GetUnrealizedGainLossForAccount(userId, accountId, currency, dateEnd);
                return accountAppreciation is not null ? [accountAppreciation] : [];
            },
            () => valuationHttpClient.GetBenchmarkSeriesAsync(
                accountId,
                currency.Id,
                dateStart,
                dateEnd,
                benchmark?.ListingId),
            async () => await moneyFlowHttpClient.GetCapital(userId, currency, dateStart, dateEnd, [accountId]));
        var holdingMetadata = await holdingMetadataTask;

        var series = chartRequests.Series;
        var holdings = chartRequests.Holdings;
        var appreciation = chartRequests.Appreciation.SingleOrDefault(x => x.AccountId == accountId);

        // Keep the full ordered series for balance maths; trim only leading zeros for the chart so
        // a range that starts before the first holding still reports the true change from zero.
        var orderedSeries = series
            .OrderBy(kv => kv.Key)
            .Select(kv => new TimeSeriesModel(kv.Key, kv.Value))
            .ToList();
        // The benchmark tracks the account's own contributions, so it is asked for the whole range
        // and starts itself on the day the account first holds something — no base point to seed.
        var benchmarkSeries = chartRequests.BenchmarkSeries
            .OrderBy(x => x.Key)
            .Select(x => new TimeSeriesModel(x.Key, x.Value))
            .ToList();
        var capitalSeries = ChartHelper.EnsureSeriesEndsAt(chartRequests.CapitalSeries, dateEnd);
        var currentBalance = orderedSeries.LastOrDefault()?.Value ?? 0;

        // Capital value (remaining buy cost) and current valuation are the two source-of-truth
        // figures; the gain/loss shown in the hero and the breakdown card is derived purely from
        // their difference so the number can never disagree with the two amounts on display.
        var capitalValue = appreciation?.CostBasis ?? 0m;
        var currentValue = appreciation?.CurrentValue ?? 0m;
        var balanceChange = currentValue - capitalValue;
        return new InvestmentAccountChartModel(
            selectedRange,
            dateStart,
            dateEnd,
            [.. orderedSeries.SkipWhile(x => x.Value == 0)],
            benchmarkSeries,
            benchmarkName,
            currentBalance,
            capitalValue,
            currentValue,
            balanceChange,
            capitalValue == 0m ? null : balanceChange / capitalValue * 100m,
            BuildHoldings(holdingMetadata, holdings, dateEnd),
            capitalSeries);
    }

    internal static List<InvestmentHoldingModel> BuildHoldings(
        IReadOnlyList<InvestmentTransactionDto> holdingMetadata,
        IReadOnlyDictionary<long, decimal> holdings,
        DateTime asOf)
    {
        // Value each holding from its latest trade on or before the as-of date so historical
        // ranges don't pull ticker/price metadata from trades that happen after the range end.
        var asOfDate = DateOnly.FromDateTime(asOf);
        var latestByListing = holdingMetadata
            .Where(t => t.TradeDate <= asOfDate)
            .GroupBy(t => t.AssetListingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.Id).First());

        var rows = new List<InvestmentHoldingModel>();
        foreach (var (listingId, quantity) in holdings)
        {
            if (quantity == 0m || !latestByListing.TryGetValue(listingId, out var latest)) continue;
            rows.Add(new InvestmentHoldingModel(
                listingId,
                latest.Ticker,
                latest.ExchangeName,
                latest.Currency,
                quantity,
                latest.UnitPrice,
                quantity * latest.UnitPrice,
                latest.AssetType));
        }
        return [.. rows.OrderByDescending(h => h.Value)];
    }
}