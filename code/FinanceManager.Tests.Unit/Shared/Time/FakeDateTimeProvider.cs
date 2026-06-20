using FinanceManager.Domain.Shared.Services;

namespace FinanceManager.Tests.Unit.Shared.Time;

internal sealed class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; private set; } = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

    public void SetUtcNow(DateTime utcNow) => UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

    public void Advance(TimeSpan timeSpan) => UtcNow = UtcNow.Add(timeSpan);
}