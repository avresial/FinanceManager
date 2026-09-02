using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

/// <summary>
/// Starts the independent API requests that supply an investment account chart before awaiting any
/// one of them. Keeping the join here makes the request graph explicit and testable.
/// </summary>
public static class InvestmentChartRequestLoader
{
    public static async Task<(
        IReadOnlyDictionary<DateTime, decimal> Series,
        IReadOnlyDictionary<long, decimal> Holdings,
        IReadOnlyList<UnrealizedGainLossAccountResult> Appreciation,
        IReadOnlyDictionary<DateTime, decimal> BenchmarkSeries,
        IReadOnlyList<TimeSeriesModel> CapitalSeries)> LoadAsync(
        Func<Task<IReadOnlyDictionary<DateTime, decimal>>> fetchSeries,
        Func<Task<IReadOnlyDictionary<long, decimal>>> fetchHoldings,
        Func<Task<IReadOnlyList<UnrealizedGainLossAccountResult>>> fetchAppreciation,
        Func<Task<IReadOnlyDictionary<DateTime, decimal>>> fetchBenchmarkSeries,
        Func<Task<IReadOnlyList<TimeSeriesModel>>> fetchCapitalSeries)
    {
        var seriesTask = fetchSeries();
        var holdingsTask = fetchHoldings();
        var appreciationTask = fetchAppreciation();
        var benchmarkSeriesTask = fetchBenchmarkSeries();
        var capitalSeriesTask = fetchCapitalSeries();

        await Task.WhenAll(seriesTask, holdingsTask, appreciationTask, benchmarkSeriesTask, capitalSeriesTask);

        return (
            await seriesTask,
            await holdingsTask,
            await appreciationTask,
            await benchmarkSeriesTask,
            await capitalSeriesTask);
    }
}