using System.Globalization;

namespace FinanceManager.Application.Services;

public static class TimeBucketService
{
    public static IEnumerable<(DateTime Date, List<T> Objects)> Get<T>(IEnumerable<(DateTime Date, T Object)> dataToGroup, TimeBucket timeBucket) =>
    timeBucket switch
    {
        TimeBucket.Day => GetDaily(dataToGroup),
        TimeBucket.Week => GetWeekly(dataToGroup),
        TimeBucket.Month => GetMonthly(dataToGroup),
        TimeBucket.Year => GetYearly(dataToGroup),
        _ => throw new NotImplementedException(),
    };

    public static IEnumerable<(DateTime Date, List<T> Objects)> Get<T>(IEnumerable<(DateTime Date, T Object)> dataToGroup)
    {
        if (!dataToGroup.Any()) return [];

        var startDate = dataToGroup.Min(x => x.Date);
        var endDate = dataToGroup.Max(x => x.Date);
        var totalDays = (endDate - startDate).TotalDays;

        if (totalDays <= 31) return GetDaily(dataToGroup);
        else if (totalDays <= 3 * 31) return GetWeekly(dataToGroup);
        else if (totalDays <= 365) return GetMonthly(dataToGroup);

        return GetYearly(dataToGroup);
    }

    public static IEnumerable<(DateTime Date, List<T> Objects)> GetDaily<T>(IEnumerable<(DateTime Date, T Object)> dataToGroup) => dataToGroup.GroupBy(row => new
    {
        row.Date.Year,
        row.Date.Month,
        row.Date.Day
    }).Select(x => (x.First().Date.Date, x.Select(y => y.Object).ToList()));

    public static IEnumerable<(DateTime Date, List<T>)> GetWeekly<T>(IEnumerable<(DateTime Date, T Object)> dataToGroup)
    {
        var dateTimeFormatInfo = DateTimeFormatInfo.CurrentInfo;
        return dataToGroup.GroupBy(row => dateTimeFormatInfo.Calendar.GetWeekOfYear(Convert.ToDateTime(row.Date), dateTimeFormatInfo.CalendarWeekRule, dateTimeFormatInfo.FirstDayOfWeek))
            .Select(x => (x.First().Date.Date, x.Select(y => y.Object).ToList()));
    }

    public static IEnumerable<(DateTime Date, List<T> Objects)> GetMonthly<T>(IEnumerable<(DateTime Date, T Object)> dataToGroup) => dataToGroup.GroupBy(row => new
    {
        row.Date.Year,
        row.Date.Month,
    }).Select(x => (x.First().Date.Date, x.Select(y => y.Object).ToList()));

    public static IEnumerable<(DateTime Date, List<T> Objects)> GetYearly<T>(IEnumerable<(DateTime Date, T Object)> dataToGroup) => dataToGroup.GroupBy(row => new
    {
        row.Date.Year,
    }).Select(x => (x.First().Date.Date, x.Select(y => y.Object).ToList()));

}