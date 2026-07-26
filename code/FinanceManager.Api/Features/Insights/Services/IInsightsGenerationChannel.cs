namespace FinanceManager.Api.Features.Insights.Services;

public interface IInsightsGenerationChannel
{
    ValueTask QueueUser(int userId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<int> ReadAll(CancellationToken cancellationToken);
}