using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Api.Features.Labels.Services;

internal sealed class LabelSetterStartupService(
    IServiceScopeFactory scopeFactory,
    ILabelSetterChannel labelSetterChannel, IConfiguration configuration,
    ILogger<LabelSetterStartupService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            logger.LogInformation("Label setter startup scan started.");

            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();


                var defaultUserLogin = configuration["DefaultUser:Login"];
                var excludedUserId = string.IsNullOrWhiteSpace(defaultUserLogin)
                    ? null
                    : (await userRepository.GetUser(defaultUserLogin))?.UserId;

                // Owned entries without a label, linked to an account or not. Ephemeral guest sandboxes (negative ids)
                // and the configured demo user never go through the shared AI labeling queue.
                var unlabeledEntries = await dbContext.CurrencyEntries.AsNoTracking()
                    .Where(entry => entry.UserId != null && entry.UserId > 0
                        && (excludedUserId == null || entry.UserId != excludedUserId)
                        && !entry.Labels.Any()
                        && (entry.Description != ""
                            || (entry.ContractorDetails != null && entry.ContractorDetails != "")))
                    .OrderBy(entry => entry.UserId).ThenBy(entry => entry.EntryId)
                    .Select(entry => new { UserId = entry.UserId!.Value, entry.EntryId })
                    .ToListAsync(cancellationToken);

                if (unlabeledEntries.Count == 0)
                {
                    logger.LogInformation("No unlabeled currency entries found on startup.");
                    logger.LogInformation("Label setter startup scan completed.");
                    return;
                }

                // One job per user — the background service already chunks into AI-sized
                // batches internally, so pre-splitting here just multiplies queued-job count.
                foreach (var group in unlabeledEntries.GroupBy(entry => entry.UserId))
                {
                    var entryIds = group.Select(entry => entry.EntryId).ToList();
                    logger.LogDebug(
                        "Queueing {Count} unlabeled entries as a single job for user {UserId}.",
                        entryIds.Count,
                        group.Key);
                    await labelSetterChannel.QueueEntries(group.Key, entryIds, cancellationToken);
                }

                logger.LogInformation(
                    "Queued {Count} unlabeled entries across {Users} users for labeling on startup.",
                    unlabeledEntries.Count,
                    unlabeledEntries.Select(entry => entry.UserId).Distinct().Count());
                logger.LogInformation("Label setter startup scan completed.");
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Label setter startup scan cancelled during application shutdown.");
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Label setter startup scan cancelled or timed out.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to queue unlabeled currency entries on startup.");
            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

}