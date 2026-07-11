using FinanceManager.Domain.Shared.Services;

namespace FinanceManager.Application.Shared.Time;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}