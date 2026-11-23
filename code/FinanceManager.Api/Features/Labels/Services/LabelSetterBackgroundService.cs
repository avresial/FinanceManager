using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.Labels.Setter;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Features.Labels.Services;

public sealed class LabelSetterBackgroundService(
    ILabelSetterChannel channel,
    IServiceScopeFactory scopeFactory,
    ILabelSetterProgressTracker progressTracker,
    ILogger<LabelSetterBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Label setter background service started.");

        await foreach (var request in channel.ReadAll(stoppingToken))
        {
            // Guest sandboxes (negative ids) are ephemeral and never run background AI labeling against the shared database.
            if (request.UserId <= 0 || request.EntryIds.Count == 0) continue;

            logger.LogDebug(
                "Processing label assignment for user {UserId} with {Count} entries. Entry IDs range: {FirstEntryId} - {LastEntryId}",
                request.UserId,
                request.EntryIds.Count,
                request.EntryIds.First(),
                request.EntryIds.Last());

            try
            {
                using var scope = scopeFactory.CreateScope();

                var labelSetterAiService = scope.ServiceProvider.GetRequiredService<ILabelSetterAiService>();
                var currencyEntryRepository = scope.ServiceProvider.GetRequiredService<IAccountEntryRepository<CurrencyAccountEntry>>();
                var financialLabelsRepository = scope.ServiceProvider.GetRequiredService<IFinancialLabelsRepository>();
                var financialAlertService = scope.ServiceProvider.GetRequiredService<IFinancialAlertService>();

                // Build name → id lookup once
                var allLabels = await financialLabelsRepository
                    .GetLabels(stoppingToken)
                    .ToListAsync(stoppingToken);

                var labelsById = allLabels.ToDictionary(l => l.Name, l => l.Id, StringComparer.Ordinal);

                progressTracker.StartJob(request.UserId, request.EntryIds.Count);

                // Pre-calculate batch count
                var batches = request.EntryIds.Chunk(20).ToList();
                logger.LogInformation(
                    "Adding labels for user {UserId} started. Processing {TotalEntries} entries in {BatchCount} batches.",
                    request.UserId,
                    request.EntryIds.Count,
                    batches.Count);

                var totalProcessed = 0;
                var currentBatchNumber = 0;

                foreach (var entryIdBatch in batches)
                {
                    currentBatchNumber++;
                    totalProcessed += entryIdBatch.Length;

                    logger.LogInformation(
                        "Batch {BatchNumber}/{TotalBatches} started for user {UserId}. Processing {BatchSize} entries, with Entry IDs range: {FirstEntryId} - {LastEntryId}.",
                        currentBatchNumber,
                        batches.Count,
                        request.UserId,
                        entryIdBatch.Length,
                        entryIdBatch.First(),
                        entryIdBatch.Last());

                    // Get AI label assignments for this batch
                    var assignments = await labelSetterAiService.AssignLabels(request.UserId, entryIdBatch, stoppingToken);
                    if (assignments.Count == 0)
                    {
                        logger.LogDebug(
                            "No label assignments returned for user {UserId} batch {BatchNumber}/{TotalBatches}.",
                            request.UserId,
                            currentBatchNumber,
                            batches.Count);
                        progressTracker.ReportBatchCompleted(entryIdBatch.Length);
                        continue;
                    }

                    // Build list of valid label assignments
                    var validAssignments = new List<(long entryId, int labelId)>();
                    foreach (var (entryId, labelName) in assignments)
                    {
                        if (!labelsById.TryGetValue(labelName, out var labelId))
                        {
                            logger.LogTrace(
                                "Skipping unknown label '{LabelName}' for entry {EntryId} for user {UserId}.",
                                labelName,
                                entryId,
                                request.UserId);
                            continue;
                        }

                        validAssignments.Add((entryId, labelId));
                    }

                    // Persist all labels for this batch at once
                    if (validAssignments.Count > 0)
                    {
                        var addedCount = await currencyEntryRepository.AddLabels(request.UserId, validAssignments, stoppingToken);
                        logger.LogDebug(
                            "Persisted {AddedCount} labels for {BatchSize} entries in batch {BatchNumber}/{TotalBatches} for user {UserId}.",
                            addedCount,
                            entryIdBatch.Length,
                            currentBatchNumber,
                            batches.Count,
                            request.UserId);
                    }

                    progressTracker.ReportBatchCompleted(entryIdBatch.Length);
                }

                logger.LogInformation(
                    "Adding labels for user {UserId} finished. Successfully processed {TotalProcessed}/{TotalEntries} entries in {BatchCount} batches.",
                    request.UserId,
                    totalProcessed,
                    request.EntryIds.Count,
                    batches.Count);

                await financialAlertService.EvaluateAlertsAsync(request.UserId, CancellationToken.None);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Label assignment cancelled during application shutdown for user {UserId}.", request.UserId);
                break;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Label assignment cancelled or timed out for user {UserId}.", request.UserId);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error occurred in label setter background worker for user {UserId} and {Count} entries.",
                    request.UserId,
                    request.EntryIds.Count);
            }
            finally
            {
                progressTracker.CompleteJob();
            }
        }

        logger.LogInformation("Label setter background service stopped.");
    }
}