using System.Globalization;

namespace FinanceManager.Components.Shared.Helpers;

/// <summary>
/// Formats dates for use inside cache keys.
/// </summary>
/// <remarks>
/// Cache keys must never carry sub-day precision. The dates that reach them are routinely clock reads —
/// a card's "as of" instant, or a range end clamped to now by <see cref="DateRangeHelper"/> — so a
/// minute- or tick-precise segment yields a brand new key on every visit: the entry just written is never
/// read again, the cache never hits, and local storage grows an orphaned entry per navigation. Bucketing
/// to the day keeps the key stable for as long as the data is worth reusing; freshness within the day
/// remains the job of each cache's own staleness check.
/// </remarks>
public static class CacheKeyDate
{
    /// <summary>
    /// Renders <paramref name="value"/> as a day-precision cache key segment.
    /// </summary>
    public static string ToSegment(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}