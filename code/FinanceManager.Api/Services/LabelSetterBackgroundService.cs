using FinanceManager.Application.Services.Ai;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Services;

public sealed class LabelSetterBackgroundService(
    ILabelSetterChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<LabelSetterBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Label setter background service started.");

        await foreach (var request in channel.ReadAll(stoppingToken))
        {
            if (request.EntryIds.Count == 0) continue;

            logger.LogDebug(
                "Processing label assignment for account {AccountId} with {Count} entries.",
                request.AccountId,
                request.EntryIds.Count);

            try
            {
                using var scope = scopeFactory.CreateScope();

                var labelSetterAiService = scope.ServiceProvider.GetRequiredService<ILabelSetterAiService>();
                var currencyEntryRepository = scope.ServiceProvider.GetRequiredService<IAccountEntryRepository<CurrencyAccountEntry>>();
                var financialLabelsRepository = scope.ServiceProvider.GetRequiredService<IFinancialLabelsRepository>();

                // Build name → id lookup once
                var allLabels = await financialLabelsRepository
                    .GetLabels(stoppingToken)
                    .ToListAsync(stoppingToken);

                var labelsById = allLabels.ToDictionary(l => l.Name, l => l.Id, StringComparer.Ordinal);

                var totalAssignments = 0;

                foreach (var entryIdBatch in request.EntryIds.Chunk(100))
                {
                    // Ask AI for label assignments (only existing labels are returned)
                    var assignments = await labelSetterAiService.AssignLabels(entryIdBatch, stoppingToken);
                    if (assignments.Count == 0)
                    {
                        logger.LogDebug(
                            "No label assignments returned for account {AccountId} for batch size {BatchSize}.",
                            request.AccountId,
                            entryIdBatch.Length);
                        continue;
                    }

                    // Build list of valid label assignments
                    var validAssignments = new List<(int entryId, int labelId)>();
                    foreach (var (entryId, labelName) in assignments)
                    {
                        if (!labelsById.TryGetValue(labelName, out var labelId))
                        {
                            logger.LogTrace(
                                "Skipping unknown label '{LabelName}' for entry {EntryId} in account {AccountId}.",
                                labelName,
                                entryId,
                                request.AccountId);
                            continue;
                        }

                        validAssignments.Add((entryId, labelId));
                    }

                    // Add all labels in one batch
                    if (validAssignments.Count > 0)
                    {
                        var addedCount = await currencyEntryRepository.AddLabels(validAssignments, stoppingToken);
                        logger.LogDebug(
                            "Added {Count} labels for {BatchSize} entries in account {AccountId}.",
                            addedCount,
                            entryIdBatch.Length,
                            request.AccountId);
                    }

                    totalAssignments += assignments.Count;
                }

                logger.LogInformation(
                    "Finished label assignment for account {AccountId}. Assignments: {Count}.",
                    request.AccountId,
                    totalAssignments);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error occurred in label setter background worker for account {AccountId} and {Count} entries.",
                    request.AccountId,
                    request.EntryIds.Count);
            }
        }

        logger.LogInformation("Label setter background service stopped.");
    }
}
