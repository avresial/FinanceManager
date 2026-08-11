namespace FinanceManager.Components.Shared.Helpers;

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

    /// <summary>
    /// A rolling window ending at <paramref name="now"/> that covers the last 31 calendar days,
    /// counting today — opened on 10 Aug this yields 11 Jul 00:00 (UTC) → now. Start is anchored to
    /// midnight so the first day buckets whole. Unlike <see cref="GetCurrentMonthRange"/> this always
    /// carries about a month of history regardless of the day of the month, so charts are not nearly
    /// empty at month boundaries.
    /// </summary>
    public static (DateTime Start, DateTime End) GetLast31DaysRange(DateTime now)
    {
        var start = now.Date.AddDays(-30);
        return (start, now);
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