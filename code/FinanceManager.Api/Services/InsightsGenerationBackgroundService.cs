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
        logger.LogInformation("Insights generation background service started.");

        await foreach (var userId in channel.ReadAll(stoppingToken))
        {
            try
            {
                logger.LogDebug("Processing insights generation for user {UserId}.", userId);

                using var scope = scopeFactory.CreateScope();
                var financialInsightsRepository = scope.ServiceProvider.GetRequiredService<IFinancialInsightsRepository>();
                var financialInsightsAiGenerator = scope.ServiceProvider.GetRequiredService<IFinancialInsightsAiGenerator>();

                var latest = await financialInsightsRepository.GetLatestByUser(userId, 1, cancellationToken: stoppingToken);
                var hasRecent = latest.Any(x => x.CreatedAt >= DateTime.UtcNow.AddHours(-24));
                if (hasRecent)
                {
                    logger.LogDebug("Skipping insights generation for user {UserId}; recent insights found.", userId);
                    continue;
                }

                var insights = await financialInsightsAiGenerator.GenerateInsights(userId, null, _insightsCountToGenerate, stoppingToken);
                if (insights.Count == 0)
                {
                    logger.LogDebug("No insights generated for user {UserId}.", userId);
                    continue;
                }

                await financialInsightsRepository.AddRange(insights, stoppingToken);
                logger.LogInformation("Stored {Count} insights for user {UserId}.", insights.Count, userId);
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

        logger.LogInformation("Insights generation background service stopped.");
    }
}