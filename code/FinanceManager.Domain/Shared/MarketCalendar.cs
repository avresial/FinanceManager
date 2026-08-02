namespace FinanceManager.Domain.Shared;

/// <summary>
/// Weekday-only trading calendar shared by the pricing paths. Exchanges (and the FX venues behind
/// them) are closed on Saturday and Sunday, so no provider ever publishes a close for those dates:
/// asking for them spends a rate-limited provider call to get zero rows back, and the window is
/// still "missing" next time so the request repeats. Weekend dates therefore resolve to the
/// preceding Friday.
///
/// Public holidays are deliberately not modelled — they differ per exchange and callers already
/// carry the latest known quote forward across any gap, so a holiday costs at most one wasted call.
/// </summary>
public static class MarketCalendar
{
    /// <summary>True when the exchange is open on <paramref name="date"/> (Monday–Friday).</summary>
    public static bool IsMarketDay(DateTime date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>
    /// The most recent market day at or before <paramref name="date"/>: a Saturday or Sunday maps
    /// back to that week's Friday, every other day maps to itself.
    /// </summary>
    public static DateTime LastMarketDay(DateTime date)
    {
        var result = date.Date;
        while (!IsMarketDay(result))
            result = result.AddDays(-1);

        return result;
    }

    /// <summary>
    /// The first market day at or after <paramref name="date"/>: a Saturday or Sunday maps forward
    /// to the following Monday, every other day maps to itself.
    /// </summary>
    public static DateTime NextMarketDay(DateTime date)
    {
        var result = date.Date;
        while (!IsMarketDay(result))
            result = result.AddDays(1);

        return result;
    }

    /// <summary>
    /// True when [<paramref name="start"/>, <paramref name="end"/>] contains at least one market
    /// day. A window that does not is unfetchable, not merely uncovered.
    /// </summary>
    public static bool HasMarketDay(DateTime start, DateTime end) =>
        start.Date <= end.Date && NextMarketDay(start) <= end.Date;
}