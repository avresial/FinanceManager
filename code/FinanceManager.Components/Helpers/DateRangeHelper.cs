namespace FinanceManager.Components.Helpers;

public static class DateRangeHelper
{
    public static (DateTime Start, DateTime End) GetCurrentMonthRange()
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1).AddTicks(-1);
        return (start, end);
    }

    public static (DateTime Start, DateTime End) GetCurrentQuarterRange()
    {
        var now = DateTime.UtcNow;
        int currentQuarter = (now.Month - 1) / 3 + 1;
        int startMonth = (currentQuarter - 1) * 3 + 1;

        DateTime start = new(now.Year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(3).AddTicks(-1);
        return (start, end);
    }

    public static (DateTime Start, DateTime End) GetCurrentYearRange()
    {
        DateTime start = new(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddYears(1).AddTicks(-1);
        return (start, end);
    }
}