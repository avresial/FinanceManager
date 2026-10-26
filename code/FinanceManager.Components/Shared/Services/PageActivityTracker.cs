namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// Counts the API requests the client has in flight so the page can tell testing agents and scripts when it has
/// settled (see <c>PageReadinessSignal</c> and <c>docs/agents/features.json</c>). Every page loads its data through
/// the one app <see cref="HttpClient"/>, so counting requests there covers all pages, including snapshot refreshes.
/// </summary>
public sealed class PageActivityTracker
{
    private int _pending;

    public event Action? Changed;

    public bool IsIdle => Volatile.Read(ref _pending) == 0;

    public IDisposable Begin()
    {
        Interlocked.Increment(ref _pending);
        Changed?.Invoke();
        return new Activity(this);
    }

    private void End()
    {
        Interlocked.Decrement(ref _pending);
        Changed?.Invoke();
    }

    private sealed class Activity(PageActivityTracker tracker) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                tracker.End();
        }
    }
}
