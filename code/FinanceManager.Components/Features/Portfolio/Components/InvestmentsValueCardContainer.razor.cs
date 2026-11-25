using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Features.Portfolio.Models;
using FinanceManager.Components.Features.Transactions.Investments.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Portfolio.Components;

public partial class InvestmentsValueCardContainer
{
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; }
    [Inject] public required UserSettingsService UserSettings { get; set; }
    [Inject] public required InvestmentValuationHttpClient Valuation { get; set; }
    [Inject] public required AssetsHttpClient AssetsClient { get; set; }
    [Inject] public required InvestmentTradesHttpClient Trades { get; set; }

    protected override Task OnParametersSetAsync() => Reload();
    private Task Reload() => Refresh(StartDateTime.Date, EndDateTime);

    protected override async Task<(string Key, Func<Task<InvestmentsValueModel>> Fetch)> GetRequestAsync(int userId, DateTime start, DateTime end)
    {
        var currency = await UserSettings.GetCurrencyAsync();
        var benchmark = await UserSettings.GetBenchmarkAsync();
        var benchmarkName = benchmark?.Ticker ?? "Capital + inflation";
        var key = $"investments-value:v2:{userId}:{currency.Id}:{benchmark?.ListingId.ToString() ?? "inflation"}";
        return (key, Fetch);

        async Task<InvestmentsValueModel> Fetch()
        {
            var value = Valuation.GetValueSeriesAsync(currency.Id, start, end, requireSuccess: true);
            var benchmarkValues = Valuation.GetBenchmarkSeriesAsync(currency.Id, start, end, benchmark?.ListingId, requireSuccess: true);
            var capital = Valuation.GetCapitalSeriesAsync(currency.Id, start, end);
            var appreciation = AssetsClient.GetInvestmentGainLossAsync(userId, currency, end);
            // The Holdings card's reader: position presence must not depend on whether a price or FX rate exists.
            var holdings = Trades.GetHoldingsAsync(DateOnly.FromDateTime(end));
            await Task.WhenAll(value, benchmarkValues, capital, appreciation, holdings);
            var gain = await appreciation;
            return new InvestmentsValueModel([.. Ordered(await value).SkipWhile(p => p.Value == 0)],
                [new("Capital", ChartHelper.EnsureSeriesEndsAt(Ordered(await capital), end), CardChartColors.Capital),
                 new(benchmarkName, Ordered(await benchmarkValues), CardChartColors.Benchmark)],
                gain?.CurrentValue ?? 0m, gain?.CostBasis ?? 0m, currency.ShortName, benchmarkName,
                (await holdings).Count(h => h.Units != 0), gain?.ExcludedInstrumentsCount ?? 0);
        }
    }

    // Position presence drives the empty caption; price availability only decides whether a cost basis can be shown.
    private string? Caption => Model switch
    {
        null => null,
        { HasOpenPositions: false } => "No open positions",
        { HasPricedPositions: false } => "Cost basis unavailable",
        { ExcludedPositionCount: > 0 } m => $"Cost basis {MoneyFormatter.Format(m.CostBasis, m.Currency)} · {m.ExcludedPositionCount} not priced",
        var m => $"Cost basis {MoneyFormatter.Format(m.CostBasis, m.Currency)}"
    };

    private static List<TimeSeriesModel> Ordered(IReadOnlyDictionary<DateTime, decimal> series) =>
        [.. series.OrderBy(p => p.Key).Select(p => new TimeSeriesModel(p.Key, p.Value))];
}