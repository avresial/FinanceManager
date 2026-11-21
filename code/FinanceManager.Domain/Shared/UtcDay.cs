namespace FinanceManager.Domain.Shared;

/// <summary>
/// Calendar-day boundaries in UTC. Day-bucketed queries use an inclusive upper bound
/// (<see cref="EndOf"/>) because repositories filter with <c>&gt;=</c> / <c>&lt;=</c>.
/// </summary>
public static class UtcDay
{
    /// <summary>The last tick of <paramref name="instant"/>'s calendar day, as UTC.</summary>
    public static DateTime EndOf(DateTime instant) =>
        DateTime.SpecifyKind(instant.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
}