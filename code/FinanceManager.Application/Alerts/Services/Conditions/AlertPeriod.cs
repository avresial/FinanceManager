using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Shared;

namespace FinanceManager.Application.Alerts.Services.Conditions;

internal static class AlertPeriod
{
    public static (DateTime Start, DateTime End) GetRange(AlertEvaluationPeriod period, DateTime evaluationDate) => period switch
    {
        AlertEvaluationPeriod.CurrentMonth => (CalendarMonth.Of(evaluationDate).Start(), evaluationDate),
        AlertEvaluationPeriod.Last30Days => (evaluationDate.Date.AddDays(-30), evaluationDate),
        AlertEvaluationPeriod.Last7Days => (evaluationDate.Date.AddDays(-7), evaluationDate),
        AlertEvaluationPeriod.AllTime => (DateTime.MinValue, evaluationDate),
        _ => (DateTime.MinValue, evaluationDate)
    };

    public static string FormatKey(AlertEvaluationPeriod period, DateTime start, DateTime end) => period switch
    {
        AlertEvaluationPeriod.CurrentMonth => $"{start:yyyy-MM}",
        AlertEvaluationPeriod.Last30Days => $"30d-{end:yyyyMMdd}",
        AlertEvaluationPeriod.Last7Days => $"7d-{end:yyyyMMdd}",
        _ => "AllTime"
    };

    public static string FormatLabel(AlertEvaluationPeriod period) => period switch
    {
        AlertEvaluationPeriod.CurrentMonth => "current month",
        AlertEvaluationPeriod.Last30Days => "last 30 days",
        AlertEvaluationPeriod.Last7Days => "last 7 days",
        AlertEvaluationPeriod.AllTime => "all time",
        _ => period.ToString()
    };
}