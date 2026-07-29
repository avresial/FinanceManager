namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IInflationIndexProvider
{
    Task<IReadOnlyDictionary<DateTime, decimal>> GetIndexSeriesAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default);
}