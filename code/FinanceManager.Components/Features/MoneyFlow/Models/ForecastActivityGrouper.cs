using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.MoneyFlow.Models;

public static class ForecastActivityGrouper
{
    /// <summary>
    /// Orders the expected transactions by date, groups them by calendar month and computes the
    /// running balance after each one, starting from <paramref name="startingBalance"/>.
    /// </summary>
    public static IReadOnlyList<ForecastActivityMonth> Group(
        IEnumerable<CashFlowForecastTransaction> transactions,
        decimal? startingBalance)
    {
        var balance = startingBalance;
        var months = new List<ForecastActivityMonth>();

        foreach (var month in transactions
                     .OrderBy(transaction => transaction.Date)
                     .GroupBy(transaction => new DateTime(transaction.Date.Year, transaction.Date.Month, 1)))
        {
            var rows = new List<ForecastActivityRow>();
            foreach (var transaction in month)
            {
                balance += transaction.Amount;
                rows.Add(new ForecastActivityRow(transaction, balance));
            }

            months.Add(new ForecastActivityMonth(month.Key, rows));
        }

        return months;
    }

    /// <summary>Today's balance: the first forecast point, or the last historical one when the forecast is empty.</summary>
    public static decimal? StartingBalance(CashFlowForecast forecast) =>
        forecast.ForecastSeries.OrderBy(point => point.DateTime).FirstOrDefault()?.Value
        ?? forecast.HistoricalSeries.OrderBy(point => point.DateTime).LastOrDefault()?.Value;
}