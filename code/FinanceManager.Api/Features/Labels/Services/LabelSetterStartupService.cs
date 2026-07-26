using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Contexts;
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

                // One job per account — the background service already chunks into AI-sized
                // batches internally, so pre-splitting here just multiplies queued-job count.
                foreach (var group in unlabeledEntries.GroupBy(entry => entry.AccountId))
                {
                    var entryIds = group.Select(entry => entry.EntryId).ToList();
                    logger.LogDebug(
                        "Queueing {Count} unlabeled entries as a single job for account {AccountId}.",
                        entryIds.Count,
                        group.Key);
                    await labelSetterChannel.QueueEntries(group.Key, entryIds, cancellationToken);
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
        }, cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

}