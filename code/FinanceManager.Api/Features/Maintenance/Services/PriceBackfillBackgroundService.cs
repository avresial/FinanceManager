using FinanceManager.Application.Assets.Pricing;

namespace FinanceManager.Api.Features.Maintenance.Services;

/// <summary>
/// Processes queued price-backfill runs off the request path. The trigger endpoint answers 202
/// immediately; this worker then walks every held listing through the rate-limited provider chain,
/// which can take far longer than any caller (the scheduled GitHub Actions curl) should wait.
/// </summary>
public sealed class PriceBackfillBackgroundService(
    IPriceBackfillJobChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<PriceBackfillBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Price backfill background service started.");
        await foreach (var request in channel.ReadAll(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var backfillService = scope.ServiceProvider.GetRequiredService<IInvestmentPriceBackfillService>();
                await backfillService.BackfillAsync(request.Start, request.End, stoppingToken);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Price backfill cancelled during application shutdown for {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.", request.Start, request.End);
                break;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Price backfill cancelled or timed out for {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.", request.Start, request.End);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Price backfill run for {Start:yyyy-MM-dd}..{End:yyyy-MM-dd} failed.", request.Start, request.End);
            }
        }

        logger.LogInformation("Price backfill background service stopped.");
    }
}