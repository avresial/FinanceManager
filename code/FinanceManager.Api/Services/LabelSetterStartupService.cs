using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Api.Services;

internal sealed class LabelSetterStartupService(
    IServiceScopeFactory scopeFactory,
    ILabelSetterChannel labelSetterChannel, IConfiguration configuration,
    ILogger<LabelSetterStartupService> logger) : IHostedService
{
    private const int _maxEntriesPerBatch = 200;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Label setter startup scan started.");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();


            var guestUser = await userRepository.GetUser(configuration["DefaultUser:Login"]!);

            var unlabeledQuery = from entry in dbContext.CurrencyEntries.AsNoTracking()
                                .Include(x => x.Labels)
                                 join account in dbContext.Accounts.AsNoTracking()
                                     on entry.AccountId equals account.AccountId
                                 where !entry.Labels.Any()
                                     && account.AccountType == AccountType.Currency
                                     && (entry.Description != ""
                                         || (entry.ContractorDetails != null && entry.ContractorDetails != ""))
                                 select new { entry.AccountId, entry.EntryId, account.UserId };


            var unlabeledEntries = await unlabeledQuery
                .Select(entry => new { entry.AccountId, entry.EntryId })
                .ToListAsync(cancellationToken);

            if (guestUser is not null)
                unlabeledEntries.RemoveAll(entry => entry.AccountId == guestUser.UserId);

            if (unlabeledEntries.Count == 0)
            {
                logger.LogInformation("No unlabeled currency entries found on startup.");
                logger.LogInformation("Label setter startup scan completed.");
                return;
            }

            foreach (var group in unlabeledEntries.GroupBy(entry => entry.AccountId))
            {
                logger.LogDebug(
                    "Queueing {Count} unlabeled entries for account {AccountId}.",
                    group.Count(),
                    group.Key);

                foreach (var batch in group.Select(entry => entry.EntryId).Chunk(_maxEntriesPerBatch))
                {
                    logger.LogTrace(
                        "Queueing label batch for account {AccountId} with {Count} entries.",
                        group.Key,
                        batch.Length);
                    await labelSetterChannel.QueueEntries(group.Key, batch, cancellationToken);
                }
            }

            logger.LogInformation(
                "Queued {Count} unlabeled entries across {Accounts} accounts for labeling on startup.",
                unlabeledEntries.Count,
                unlabeledEntries.Select(entry => entry.AccountId).Distinct().Count());
            logger.LogInformation("Label setter startup scan completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue unlabeled currency entries on startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

}
