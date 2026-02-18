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
        await foreach (var entryIds in channel.ReadAll(stoppingToken))
        {
            if (entryIds.Count == 0) continue;

            try
            {
                using var scope = scopeFactory.CreateScope();

                var labelSetterAiService = scope.ServiceProvider.GetRequiredService<ILabelSetterAiService>();
                var currencyEntryRepository = scope.ServiceProvider.GetRequiredService<IAccountEntryRepository<CurrencyAccountEntry>>();
                var financialLabelsRepository = scope.ServiceProvider.GetRequiredService<IFinancialLabelsRepository>();

                // Ask AI for label assignments (only existing labels are returned)
                var assignments = await labelSetterAiService.AssignLabels(entryIds, stoppingToken);
                if (assignments.Count == 0) continue;

                // Build name → id lookup once
                var allLabels = await financialLabelsRepository
                    .GetLabels(stoppingToken)
                    .ToListAsync(stoppingToken);

                var labelsById = allLabels.ToDictionary(l => l.Name, l => l.Id, StringComparer.Ordinal);

                foreach (var (entryId, labelName) in assignments)
                {
                    if (!labelsById.TryGetValue(labelName, out var labelId))
                        continue;

                    var added = await currencyEntryRepository.AddLabel(entryId, labelId);
                    if (!added)
                        logger.LogWarning("Failed to add label '{LabelName}' to entry {EntryId}.", labelName, entryId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in label setter background worker for {Count} entries.", entryIds.Count);
            }
        }
    }
}
