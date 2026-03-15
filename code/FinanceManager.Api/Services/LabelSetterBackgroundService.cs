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

                // Pre-calculate batch count
                var batches = request.EntryIds.Chunk(50).ToList();
                logger.LogInformation(
                    "Adding labels for AccountId {AccountId} started. Processing {TotalEntries} entries in {BatchCount} batches.",
                    request.AccountId,
                    request.EntryIds.Count,
                    batches.Count);

                var totalProcessed = 0;
                var currentBatchNumber = 0;

                foreach (var entryIdBatch in batches)
                {
                    currentBatchNumber++;
                    totalProcessed += entryIdBatch.Length;

                    logger.LogInformation(
                        "Batch {BatchNumber}/{TotalBatches} started for AccountId {AccountId}. Processing {BatchSize} entries.",
                        currentBatchNumber,
                        batches.Count,
                        request.AccountId,
                        entryIdBatch.Length);

                    // Get AI label assignments for this batch
                    var assignments = await labelSetterAiService.AssignLabels(entryIdBatch, stoppingToken);
                    if (assignments.Count == 0)
                    {
                        logger.LogDebug(
                            "No label assignments returned for account {AccountId} batch {BatchNumber}/{TotalBatches}.",
                            request.AccountId,
                            currentBatchNumber,
                            batches.Count);
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

                    // Persist all labels for this batch at once
                    if (validAssignments.Count > 0)
                    {
                        var addedCount = await currencyEntryRepository.AddLabels(validAssignments, stoppingToken);
                        logger.LogDebug(
                            "Persisted {AddedCount} labels for {BatchSize} entries in batch {BatchNumber}/{TotalBatches} for account {AccountId}.",
                            addedCount,
                            entryIdBatch.Length,
                            currentBatchNumber,
                            batches.Count,
                            request.AccountId);
                    }
                }

                logger.LogInformation(
                    "Adding labels for AccountId {AccountId} finished. Successfully processed {TotalProcessed}/{TotalEntries} entries in {BatchCount} batches.",
                    request.AccountId,
                    totalProcessed,
                    request.EntryIds.Count,
                    batches.Count);
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
