namespace FinanceManager.Components.Services;

public interface IUiStateCacheService<TState, in TRefreshContext, in TCacheKey>
    where TState : class
    where TCacheKey : notnull
{
    Task<TState?> GetCachedAsync(TCacheKey cacheKey);
    Task<TState> GetOrRefreshAsync(TRefreshContext refreshContext);
    Task<TState> RefreshAsync(TRefreshContext refreshContext);
    Task InvalidateAsync(TCacheKey cacheKey);
}