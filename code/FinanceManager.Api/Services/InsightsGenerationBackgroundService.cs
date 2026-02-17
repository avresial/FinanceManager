using FinanceManager.Application.Services.FinancialInsights;
using FinanceManager.Domain.Repositories;

namespace FinanceManager.Api.Services;

public sealed class InsightsGenerationBackgroundService(
    IInsightsGenerationChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<InsightsGenerationBackgroundService> logger) : BackgroundService
{
    private const int _insightsCountToGenerate = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var userId in channel.ReadAll(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var financialInsightsRepository = scope.ServiceProvider.GetRequiredService<IFinancialInsightsRepository>();
                var financialInsightsAiGenerator = scope.ServiceProvider.GetRequiredService<IFinancialInsightsAiGenerator>();

                var latest = await financialInsightsRepository.GetLatestByUser(userId, 1, cancellationToken: stoppingToken);
                var hasRecent = latest.Any(x => x.CreatedAt >= DateTime.UtcNow.AddHours(-24));
                if (hasRecent) continue;

                var insights = await financialInsightsAiGenerator.GenerateInsights(userId, null, _insightsCountToGenerate, stoppingToken);
                if (insights.Count == 0) continue;

                await financialInsightsRepository.AddRange(insights, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in insights generation background worker for user {UserId}", userId);
            }
        }
    }
}