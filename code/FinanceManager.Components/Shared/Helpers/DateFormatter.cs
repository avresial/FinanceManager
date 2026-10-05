using System.Globalization;

namespace FinanceManager.Components.Shared.Helpers;

/// <summary>
/// The single place that turns dates into display text. Day first, month as a name, never a numeric
/// day/month pair, so <c>4 Sep</c> can never be misread as April: <c>4 Oct 2026</c>, <c>4 Sep – 4 Oct 2026</c>.
/// </summary>
/// <remarks>
/// The culture is fixed (invariant) so month names and order do not depend on the browser or OS locale.
/// </remarks>
public static class DateFormatter
{
    /// <summary>The culture used for every display date. Also pass it to date pickers so their text matches.</summary>
    public static CultureInfo Culture { get; } = CultureInfo.InvariantCulture;

    /// <summary>The date format for MudBlazor date pickers (<c>DateFormat</c>).</summary>
    public const string PickerFormat = "d MMM yyyy";

    private const string _shortFormat = "d MMM yyyy";
    private const string _dayMonthFormat = "d MMM";
    private const string _dateTimeFormat = "d MMM yyyy, HH:mm";
    private const string _monthYearFormat = "MMM yyyy";
    private const string _rangeSeparator = " – ";

    /// <summary>Short date, e.g. <c>4 Oct 2026</c>.</summary>
    public static string Format(DateTime date) => date.ToString(_shortFormat, Culture);

    /// <inheritdoc cref="Format(DateTime)"/>
    public static string Format(DateOnly date) => date.ToString(_shortFormat, Culture);

    /// <summary>Short date without the year, e.g. <c>4 Oct</c>, for groupings that already imply the year.</summary>
    public static string FormatDayMonth(DateTime date) => date.ToString(_dayMonthFormat, Culture);

    /// <inheritdoc cref="FormatDayMonth(DateTime)"/>
    public static string FormatDayMonth(DateOnly date) => date.ToString(_dayMonthFormat, Culture);

    /// <summary>Short date with time, e.g. <c>4 Oct 2026, 14:30</c>.</summary>
    public static string FormatDateTime(DateTime date) => date.ToString(_dateTimeFormat, Culture);

    /// <summary>Month and year, e.g. <c>Oct 2026</c>.</summary>
    public static string FormatMonthYear(DateTime date) => date.ToString(_monthYearFormat, Culture);

    /// <summary>Full weekday name, e.g. <c>Sunday</c>.</summary>
    public static string FormatWeekday(DateTime date) => date.ToString("dddd", Culture);

    /// <summary>Range with shared parts collapsed, e.g. <c>4–20 Oct 2026</c>, <c>4 Sep – 4 Oct 2026</c>.</summary>
    public static string FormatRange(DateTime start, DateTime end) =>
        FormatRange(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));

    /// <inheritdoc cref="FormatRange(DateTime, DateTime)"/>
    public static string FormatRange(DateOnly start, DateOnly end)
    {
        if (start == end) return Format(start);

        if (start.Year == end.Year && start.Month == end.Month)
            return $"{start.Day.ToString(Culture)}–{Format(end)}";

        if (start.Year == end.Year)
            return $"{start.ToString(_dayMonthFormat, Culture)}{_rangeSeparator}{Format(end)}";

        return $"{Format(start)}{_rangeSeparator}{Format(end)}";
    }
}