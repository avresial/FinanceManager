using FinanceManager.Application.Insights.Generation;
using FinanceManager.Application.Shared.Diagnostics;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Domain.Shared.Services;
using System.Diagnostics;

namespace FinanceManager.Api.Features.Insights.Services;

public sealed class InsightsGenerationBackgroundService(
    IInsightsGenerationChannel channel,
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<InsightsGenerationBackgroundService> logger) : BackgroundService
{
    private const int _insightsCountToGenerate = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Insights generation background service started.");

        await foreach (var userId in channel.ReadAll(stoppingToken))
        {
            using var activity = FinanceManagerTelemetry.ActivitySource.StartActivity("insights.generate");
            activity?.SetTag("user.id", userId);
            var stopwatch = Stopwatch.StartNew();
            var outcome = "success";
            try
            {
                logger.LogDebug("Processing insights generation for user {UserId}.", userId);

                using var scope = scopeFactory.CreateScope();
                var financialInsightsRepository = scope.ServiceProvider.GetRequiredService<IFinancialInsightsRepository>();
                var financialInsightsAiGenerator = scope.ServiceProvider.GetRequiredService<IFinancialInsightsAiGenerator>();

                var latest = await financialInsightsRepository.GetLatestByUser(userId, 1, cancellationToken: stoppingToken);
                if (!InsightsFreshness.IsStale(latest.FirstOrDefault()?.CreatedAt, dateTimeProvider.UtcNow))
                {
                    outcome = "skipped";
                    logger.LogDebug("Skipping insights generation for user {UserId}; insights are still fresh.", userId);
                    continue;
                }

                var insights = await financialInsightsAiGenerator.GenerateInsights(userId, null, _insightsCountToGenerate, stoppingToken);
                if (insights.Count == 0)
                {
                    outcome = "empty";
                    logger.LogDebug("No insights generated for user {UserId}.", userId);
                    continue;
                }

                await financialInsightsRepository.AddRange(insights, stoppingToken);
                activity?.SetTag("insights.count", insights.Count);
                logger.LogInformation("Stored {Count} insights for user {UserId}.", insights.Count, userId);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                outcome = "canceled";
                logger.LogDebug(ex, "Insights generation cancelled during application shutdown for user {UserId}.", userId);
                break;
            }
            catch (OperationCanceledException ex)
            {
                outcome = "canceled";
                logger.LogDebug(ex, "Insights generation cancelled or timed out for user {UserId}.", userId);
            }
            catch (Exception ex)
            {
                outcome = "failure";
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Error occurred in insights generation background worker for user {UserId}", userId);
            }
            finally
            {
                stopwatch.Stop();
                var outcomeTag = new KeyValuePair<string, object?>("outcome", outcome);
                FinanceManagerTelemetry.InsightsGenerationRuns.Add(1, outcomeTag);
                FinanceManagerTelemetry.InsightsGenerationDuration.Record(stopwatch.Elapsed.TotalMilliseconds, outcomeTag);
            }
        }

        logger.LogInformation("Insights generation background service stopped.");
    }
}