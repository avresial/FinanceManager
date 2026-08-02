namespace FinanceManager.Application.Shared.Persistence;

public interface IAtomicOperation
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}