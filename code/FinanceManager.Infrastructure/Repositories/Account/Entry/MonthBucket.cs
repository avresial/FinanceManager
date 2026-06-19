namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

internal static class MonthBucket
{
    internal static DateTime MonthStart(DateTime date) =>
        new(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    internal static DateTime NextMonthStart(DateTime date) =>
        MonthStart(date).AddMonths(1);

    // Inclusive upper bound for DB queries using >= / <= semantics.
    internal static DateTime MonthEndInclusive(DateTime monthStart) =>
        NextMonthStart(monthStart).AddTicks(-1);

    internal static string BucketKey(int userId, int accountId, DateTime monthStart) =>
        $"global:u{userId}:a{accountId}:m{monthStart:yyyyMM}";

    // All calendar months touched by [start, end] inclusive, oldest first.
    internal static IEnumerable<DateTime> TouchedMonths(DateTime start, DateTime end)
    {
        var month = MonthStart(start);
        var endMonth = MonthStart(end);
        while (month <= endMonth)
        {
            yield return month;
            month = month.AddMonths(1);
        }
    }

    // The oldest month start still eligible for bucket caching under the rolling horizon.
    internal static DateTime HorizonStart(int horizonMonths, DateTime now) =>
        MonthStart(now).AddMonths(-horizonMonths);
}