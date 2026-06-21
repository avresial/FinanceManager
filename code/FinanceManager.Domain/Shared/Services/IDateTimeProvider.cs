namespace FinanceManager.Domain.Shared.Services;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    DateTime TodayUtc => UtcNow.Date;
}