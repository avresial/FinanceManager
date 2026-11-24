using FinanceManager.Domain.Shared;

namespace FinanceManager.Components.Shared.Helpers;

public static class DateRangeHelper
{
    /// <summary>
    /// Selection key for a user-picked custom date range. Every range selector and
    /// every consumer comparing against it must use this constant — a mismatched
    /// literal silently falls through to the default range.
    /// </summary>
    public const string CustomRangeKey = "Custom";

    public static (DateTime Start, DateTime End) GetRange(
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
            "Month" => CalendarMonth.Of(now).Start(),
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

    /// <summary>Reads two picker days as local calendar days and returns the UTC instants of the start of the first and the end of the last.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) LocalDaysToUtc(DateTime startDay, DateTime endDay)
    {
        var localStart = DateTime.SpecifyKind(startDay.Date, DateTimeKind.Local);
        var localEnd = DateTime.SpecifyKind(endDay.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
        return (localStart.ToUniversalTime(), localEnd.ToUniversalTime());
    }

    public static (DateTime Start, DateTime End) GetCurrentMonthRange()
    {
        var month = CalendarMonth.Of(DateTime.UtcNow);
        return (month.Start(), month.EndInclusive());
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

    /// <summary>
    /// The range the overview pages — Dashboard, Assets and Liabilities — open with. It names the
    /// policy separately from the window it currently resolves to, so all three pages share one
    /// entry point and changing the default stays a single edit here rather than three. #700
    /// </summary>
    public static (DateTime Start, DateTime End) GetDefaultOverviewRange(DateTime now) =>
        GetLast31DaysRange(now);

    /// <summary>
    /// The range the Dashboard opens with: the same 31 days as <see cref="GetDefaultOverviewRange"/>,
    /// but ending at the end of today (UTC) rather than at <paramref name="now"/>. Entries posted today
    /// with a later time still count, and the range — and so the overview cache key — stays the same
    /// for the whole day instead of moving on every visit. It rolls over at UTC midnight.
    /// </summary>
    public static (DateTime Start, DateTime End) GetDefaultDashboardRange(DateTime now)
    {
        var (start, _) = GetDefaultOverviewRange(now);
        return (start, UtcDay.EndOf(now));
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