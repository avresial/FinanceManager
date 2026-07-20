namespace FinanceManager.Components.Helpers;

public static class DateRangeHelper
{
    /// <summary>
    /// Selection key for a user-picked custom date range. Every range selector and
    /// every consumer comparing against it must use this constant — a mismatched
    /// literal silently falls through to the default range.
    /// </summary>
    public const string CustomRangeKey = "Custom";

    public static (DateTime Start, DateTime End) GetAccountDetailsRange(
        string selection,
        DateTime? customStart,
        DateTime? customEnd,
        DateTime customFallbackStart,
        DateTime defaultStart,
        DateTime now)
    {
        if (selection == CustomRangeKey)
        {
            // Date pickers return midnight for the end day; extend it to end-of-day so
            // the selected day's entries are included, then clamp to now.
            var inclusiveEnd = customEnd is DateTime end ? end.Date.AddDays(1).AddTicks(-1) : now;
            return (customStart ?? customFallbackStart, inclusiveEnd < now ? inclusiveEnd : now);
        }

        var start = selection switch
        {
            "Month" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            "1M" => now.AddMonths(-1),
            "3M" => now.AddMonths(-3),
            "6M" => now.AddMonths(-6),
            "YTD" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => defaultStart,
        };
        return (start, now);
    }

    public static DateTime? GetExpandedStart(DateTime selectedStart, DateTime? oldestEntry) =>
        oldestEntry is DateTime oldest && oldest < selectedStart ? oldest : null;

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