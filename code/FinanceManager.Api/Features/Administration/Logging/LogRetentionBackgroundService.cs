using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Options;

namespace FinanceManager.Api.Features.Administration.Logging;

public sealed class LogRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<LogRetentionOptions> options,
    ILogger<LogRetentionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial cleanup on startup.
        await Cleanup(stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
                await Cleanup(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task Cleanup(CancellationToken cancellationToken)
    {
        using var _ = DatabaseLogger.BeginSuppression();

        try
        {
            var retentionDays = Math.Max(1, options.CurrentValue.RetentionDays);
            var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);

            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILogEntryRepository>();
            var deleted = await repository.DeleteOlderThan(cutoffUtc, cancellationToken);

            if (deleted > 0)
                logger.LogInformation("Log retention removed {Count} entries older than {Cutoff:o}.", deleted, cutoffUtc);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Log retention cleanup failed.");
        }
    }
}