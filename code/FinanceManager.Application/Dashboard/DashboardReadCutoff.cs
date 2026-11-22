namespace FinanceManager.Application.Dashboard;

/// <summary>
/// Dashboard ranges ending at the last tick of today value the entire UTC day. The underlying calculators are
/// shared with exact-time reports, so this override follows one asynchronous overview read.
/// </summary>
internal static class DashboardReadCutoff
{
    private static readonly AsyncLocal<DateTime?> _upperBound = new();

    internal static IDisposable? Enter(DateTime? upperBound)
    {
        var previous = _upperBound.Value;
        if (previous == upperBound) return null;
        _upperBound.Value = upperBound;
        return new Restore(previous);
    }

    internal static DateTime Clamp(DateTime requested, DateTime utcNow)
    {
        var upperBound = _upperBound.Value ?? utcNow;
        return requested > upperBound ? upperBound : requested;
    }

    private sealed class Restore(DateTime? previous) : IDisposable
    {
        public void Dispose() => _upperBound.Value = previous;
    }
}