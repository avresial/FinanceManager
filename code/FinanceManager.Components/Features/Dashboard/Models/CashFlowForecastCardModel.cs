using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed record CashFlowForecastCardModel(
    string Currency,
    decimal CurrentBalance,
    decimal ThirtyDayValue,
    decimal SixtyDayValue,
    decimal NinetyDayValue,
    bool HasForecastableActivity,
    int ExpectedInflowCount,
    int ExpectedOutflowCount,
    List<TimeSeriesModel>? HistoricalPoints = null,
    List<TimeSeriesModel>? ForecastPoints = null,
    TimeSeriesModel? LowestBalancePoint = null)
{
    // Derived from totals so snapshots written before these fields existed also show correct deltas.
    public decimal ThirtyDayDelta => ThirtyDayValue - CurrentBalance;
    public decimal SixtyDayDelta => SixtyDayValue - CurrentBalance;
    public decimal NinetyDayDelta => NinetyDayValue - CurrentBalance;

    public static CashFlowForecastCardModel FromForecast(CashFlowForecast forecast, string currency)
    {
        var today = forecast.AsOfDate.Date;
        var history = forecast.HistoricalSeries
            .Where(point => point.DateTime.Date >= today.AddDays(-30) && point.DateTime.Date <= today)
            .OrderBy(point => point.DateTime).ToList();
        var projected = forecast.ForecastSeries
            .Where(point => point.DateTime.Date >= today && point.DateTime.Date <= today.AddDays(90))
            .OrderBy(point => point.DateTime).ToList();
        var currentBalance = projected.FirstOrDefault()?.Value ?? 0m;
        decimal ValueAt(int horizon) => projected
            .FirstOrDefault(point => point.DateTime.Date == today.AddDays(horizon))?.Value
            ?? currentBalance;

        return new(
            currency,
            currentBalance,
            ValueAt(CashFlowForecastHorizons.ThirtyDays),
            ValueAt(CashFlowForecastHorizons.SixtyDays),
            ValueAt(CashFlowForecastHorizons.NinetyDays),
            forecast.HasForecastableActivity,
            forecast.ExpectedTransactions.Count(transaction => transaction.Amount > 0),
            forecast.ExpectedTransactions.Count(transaction => transaction.Amount < 0),
            history,
            projected,
            projected.MinBy(point => point.Value));
    }
}