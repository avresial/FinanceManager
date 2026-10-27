namespace FinanceManager.Infrastructure.Features.Assets.Providers;

internal sealed class AlphaVantageRequestBudget
{
    private readonly Lock _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _requests;

    public bool TryConsume() => TryConsume(DateTimeOffset.UtcNow);

    internal bool TryConsume(DateTimeOffset now)
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (_day != today) { _day = today; _requests = 0; }
            if (_requests >= 25) return false;
            _requests++;
            return true;
        }
    }
}