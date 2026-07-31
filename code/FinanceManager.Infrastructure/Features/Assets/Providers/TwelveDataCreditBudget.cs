using FinanceManager.Application.Shared.Options;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Features.Assets.Providers;

internal sealed class TwelveDataCreditBudget(IOptions<TwelveDataOptions> options)
{
    private readonly Lock _lock = new();
    private readonly Queue<DateTimeOffset> _minuteCredits = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _dailyCredits;

    public bool TryConsume() => TryConsume(DateTimeOffset.UtcNow);

    internal bool TryConsume(DateTimeOffset now)
    {
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (_day != today)
            {
                _day = today;
                _dailyCredits = 0;
            }

            while (_minuteCredits.TryPeek(out var usedAt) && now - usedAt >= TimeSpan.FromMinutes(1))
                _minuteCredits.Dequeue();

            var limits = options.Value;
            if (_dailyCredits >= limits.DailyCreditLimit || _minuteCredits.Count >= limits.PerMinuteCreditLimit)
                return false;

            _dailyCredits++;
            _minuteCredits.Enqueue(now);
            return true;
        }
    }
}