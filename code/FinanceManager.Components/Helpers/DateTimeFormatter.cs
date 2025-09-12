using System.Globalization;

namespace FinanceManager.Components.Helpers;

public static class DateTimeFormatter
{
    public static string ToRfc3339(this DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("{utcDateTime}", nameof(utcDateTime));

        return utcDateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK", DateTimeFormatInfo.InvariantInfo);
    }
}
