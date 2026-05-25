namespace FinanceManager.Api.Services;

public interface IInsightsGenerationChannel
{
    ValueTask QueueUser(int userId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<int> ReadAll(CancellationToken cancellationToken);
}