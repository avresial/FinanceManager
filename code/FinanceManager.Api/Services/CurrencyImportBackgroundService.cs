using FinanceManager.Api.Hubs;
using FinanceManager.Application.Services.Currencies;
using FinanceManager.Domain.Entities.Imports;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Services;

public sealed class CurrencyImportBackgroundService(
    ICurrencyImportJobChannel channel,
    ICurrencyImportJobStore jobStore,
    IServiceScopeFactory scopeFactory,
    IHubContext<CurrencyImportHub> hub,
    ILogger<CurrencyImportBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Currency import background service started.");
        DateTime lastStatusUpdate = DateTime.MinValue;
        await foreach (var request in channel.ReadAll(stoppingToken))
        {
            try
            {
                if (!jobStore.TrySetRunning(request.JobId))
                    continue;

                await PublishStatus(request.JobId, request.UserId, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<ICurrencyAccountImportService>();
                var domainEntries = request.Entries
                    .Select(x => new CurrencyEntryImport(x.PostingDate, x.ValueChange, x.ContractorDetails, x.Description))
                    .ToList();

                var result = await importService.ImportEntries(
                    request.UserId,
                    request.AccountId,
                    domainEntries,
                    onConflicts: async conflicts =>
                    {
                        foreach (var conflict in conflicts.Where(x => !x.IsExactMatch))
                        {
                            var added = jobStore.TryAddConflict(request.JobId, conflict);
                            if (added is null)
                                continue;

                            if (!added.IsResolved)
                            {
                                await hub.Clients.Group(CurrencyImportHub.GetJobGroupName(request.JobId))
                                    .SendAsync("ConflictDiscovered", added, stoppingToken);
                            }
                        }

                        await PublishStatus(request.JobId, request.UserId, stoppingToken);
                    },
                    onProgress: async (processed, imported, failed) =>
                    {
                        jobStore.TryUpdateProgress(request.JobId, processed, imported, failed);

                        var timeSinceLastUpdate = DateTime.UtcNow - lastStatusUpdate;
                        if (timeSinceLastUpdate >= TimeSpan.FromMilliseconds(200) || processed == domainEntries.Count)
                        {
                            lastStatusUpdate = DateTime.UtcNow;
                            await PublishStatus(request.JobId, request.UserId, stoppingToken);
                        }
                    });

                jobStore.TryMarkCompleted(request.JobId, result);
                await PublishStatus(request.JobId, request.UserId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Currency import job {JobId} failed.", request.JobId);
                jobStore.TryMarkFailed(request.JobId, ex.Message);
                await PublishStatus(request.JobId, request.UserId, stoppingToken);
            }
        }

        logger.LogInformation("Currency import background service stopped.");
    }

    private async Task PublishStatus(Guid jobId, int userId, CancellationToken cancellationToken)
    {
        if (!jobStore.TryGetStatus(jobId, userId, out var status) || status is null)
            return;

        await hub.Clients.Group(CurrencyImportHub.GetJobGroupName(jobId))
            .SendAsync("ImportStatusUpdated", status, cancellationToken);
    }
}