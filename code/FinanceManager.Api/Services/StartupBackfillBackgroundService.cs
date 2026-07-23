using FinanceManager.Application.Backfill;
using FinanceManager.Application.Shared.Options;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace FinanceManager.Api.Services;

/// <summary>
/// Runs every registered <see cref="IBackfillService"/> once, in the background, after the API starts.
/// It does not block API readiness, isolates each service's failures so one bad run cannot stop the
/// rest, and executes them sequentially in deterministic order because the underlying providers
/// (Alpha Vantage free tier) are heavily rate-limited.
/// </summary>
public sealed class StartupBackfillBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackfillOptions> options,
    ILogger<StartupBackfillBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunOnStartup)
        {
            logger.LogInformation("Startup backfill is disabled (Backfill:RunOnStartup=false); nothing to run.");
            return;
        }

        // Yield so host startup completes and the API is ready before any provider work begins.
        await Task.Yield();

        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider
            .GetServices<IBackfillService>()
            .OrderBy(s => s.Order)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        if (services.Count == 0)
        {
            logger.LogInformation("Startup backfill found no registered backfill services.");
            return;
        }

        logger.LogInformation("Startup backfill running {Count} service(s).", services.Count);

        foreach (var service in services)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Startup backfill cancelled before running {Service}.", service.Name);
                break;
            }

            var stopwatch = Stopwatch.StartNew();
            logger.LogInformation("Startup backfill starting {Service}.", service.Name);

            try
            {
                var result = await service.BackfillAsync(stoppingToken);
                stopwatch.Stop();

                logger.LogInformation(
                    "Startup backfill finished {Service} in {ElapsedMs} ms: {Inspected} inspected, " +
                    "{Requests} requests, {Inserted} inserted, {Skipped} skipped, {Failures} failures, rate-limited: {RateLimited}.",
                    service.Name, stopwatch.ElapsedMilliseconds, result.TargetsInspected, result.ProviderRequests,
                    result.RowsInserted, result.RowsSkipped, result.Failures, result.RateLimited);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is shutting down: stop cleanly without treating it as a service failure.
                logger.LogInformation("Startup backfill cancelled during {Service}.", service.Name);
                break;
            }
            catch (Exception ex)
            {
                // Isolate failures: a thrown service must not stop the remaining services from running.
                stopwatch.Stop();
                logger.LogError(ex, "Startup backfill service {Service} failed after {ElapsedMs} ms.", service.Name, stopwatch.ElapsedMilliseconds);
            }
        }

        logger.LogInformation("Startup backfill run complete.");
    }
}