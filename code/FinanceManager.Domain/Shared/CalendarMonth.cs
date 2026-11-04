namespace FinanceManager.Domain.Shared;

/// <summary>
/// A calendar month (year + month) and its boundaries. Month-bucketed queries use an inclusive
/// upper bound (<see cref="EndInclusive"/>) because repositories filter with <c>&gt;=</c> / <c>&lt;=</c>.
/// Boundaries default to UTC; callers that must keep the kind of an incoming date pass it explicitly.
/// </summary>
public readonly record struct CalendarMonth(int Year, int Month)
{
    public int Year { get; } = Year is >= 1 and <= 9999
        ? Year
        : throw new ArgumentOutOfRangeException(nameof(Year), Year, "Year must be between 1 and 9999.");

    public int Month { get; } = Month is >= 1 and <= 12
        ? Month
        : throw new ArgumentOutOfRangeException(nameof(Month), Month, "Month must be between 1 and 12.");

    public static CalendarMonth Of(DateTime date) => new(date.Year, date.Month);

    public static CalendarMonth Of(DateOnly date) => new(date.Year, date.Month);

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateTime Start(DateTimeKind kind = DateTimeKind.Utc) => new(Year, Month, 1, 0, 0, 0, kind);

    public DateTime EndInclusive(DateTimeKind kind = DateTimeKind.Utc) => Start(kind).AddMonths(1).AddTicks(-1);

    public CalendarMonth AddMonths(int months) => Of(FirstDay.AddMonths(months));
}