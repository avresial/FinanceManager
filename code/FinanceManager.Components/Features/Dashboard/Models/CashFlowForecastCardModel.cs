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
    int ExpectedOutflowCount)
{
    public static CashFlowForecastCardModel FromForecast(CashFlowForecast forecast, string currency)
    {
        var currentBalance = forecast.ForecastSeries.FirstOrDefault()?.Value ?? 0m;
        decimal ValueAt(int horizon) => forecast.ForecastSeries
            .FirstOrDefault(point => point.DateTime.Date == forecast.AsOfDate.AddDays(horizon).Date)?.Value
            ?? currentBalance;

        return new(
            currency,
            currentBalance,
            ValueAt(CashFlowForecastHorizons.ThirtyDays),
            ValueAt(CashFlowForecastHorizons.SixtyDays),
            ValueAt(CashFlowForecastHorizons.NinetyDays),
            forecast.HasForecastableActivity,
            forecast.ExpectedTransactions.Count(transaction => transaction.Amount > 0),
            forecast.ExpectedTransactions.Count(transaction => transaction.Amount < 0));
    }
}