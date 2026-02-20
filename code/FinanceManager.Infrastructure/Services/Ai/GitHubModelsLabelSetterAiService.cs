using FinanceManager.Application.Services.Ai;
using FinanceManager.Application.Services.Exports;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class GitHubModelsLabelSetterAiService(
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntryRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    ILabelSetterPromptProvider promptProvider,
    IAccountCsvExportService<CurrencyAccountExportDto> csvExportService,
    IAiProvider aiProvider,
    ILogger<GitHubModelsLabelSetterAiService> logger) : ILabelSetterAiService
{
    private const int _maxEntriesPerBatch = 25;
    private const string _systemPrompt = "You are a finance assistant that outputs strict JSON.";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Dictionary<int, string>> AssignLabels(
        IReadOnlyCollection<int> entryIds,
        CancellationToken cancellationToken = default)
    {
        if (entryIds.Count == 0)
            return [];

        var allLabels = await financialLabelsRepository.GetLabels(cancellationToken).ToListAsync(cancellationToken);
        if (allLabels.Count == 0)
        {
            logger.LogInformation("No labels defined in the system - skipping label assignment.");
            return [];
        }

        var availableLabels = string.Join(", ", allLabels.Select(l => l.Name));
        var labelNameSet = new HashSet<string>(allLabels.Select(l => l.Name), StringComparer.Ordinal);

        var result = new Dictionary<int, string>();

        foreach (var batch in entryIds.Chunk(_maxEntriesPerBatch))
        {
            logger.LogTrace("Processing batch with {Count} entry IDs.", batch.Length);

            var entries = await currencyEntryRepository.GetByIds(batch, cancellationToken);
            if (entries.Count == 0)
            {
                logger.LogTrace("No entries found for batch of {Count} entry IDs.", batch.Length);
                continue;
            }

            logger.LogTrace("Retrieved {Count} entries for batch.", entries.Count);

            var batchSet = new HashSet<int>(batch);
            var dtos = entries.Select(CurrencyAccountExportDto.FromEntity).ToList();
            var csv = csvExportService.GetExportResults(dtos);
            var prompt = await promptProvider.BuildPromptAsync(availableLabels, csv, cancellationToken);

            try
            {
                logger.LogTrace("Sending batch of {Count} entries to AI for label assignment.", entries.Count);

                var content = await aiProvider.Get(_systemPrompt, prompt, cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    logger.LogWarning("GitHub Models returned empty response for label setter batch.");
                    continue;
                }

                var parsed = TryParseAssignments(content);
                logger.LogTrace("Parsed {Count} assignments from AI response for batch.", parsed.Count);

                int batchAssignments = 0;
                foreach (var assignment in parsed)
                {
                    if (assignment.EntryId is null) continue;
                    if (string.IsNullOrWhiteSpace(assignment.LabelName)) continue;
                    if (!batchSet.Contains(assignment.EntryId.Value)) continue;
                    if (!labelNameSet.Contains(assignment.LabelName)) continue;

                    result[assignment.EntryId.Value] = assignment.LabelName;
                    batchAssignments++;
                }

                logger.LogTrace("Added {Count} valid assignments to result for batch.", batchAssignments);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GitHub Models label setter failed for a batch of {Count} entries.", batch.Length);
            }
        }

        return result;
    }

    private static List<AssignmentItem> TryParseAssignments(string content)
    {
        var trimmed = content.Trim();

        if (TryDeserialize(trimmed, out var parsed))
            return parsed;

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var candidate = trimmed[start..(end + 1)];
            if (TryDeserialize(candidate, out parsed))
                return parsed;
        }

        return [];

        static bool TryDeserialize(string json, out List<AssignmentItem> items)
        {
            items = [];
            try
            {
                var root = JsonSerializer.Deserialize<AssignmentsRoot>(json, _jsonOptions);
                if (root?.Assignments is null) return false;
                items = root.Assignments;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private sealed class AssignmentsRoot
    {
        [JsonPropertyName("assignments")]
        public List<AssignmentItem> Assignments { get; set; } = [];
    }

    private sealed class AssignmentItem
    {
        [JsonPropertyName("entryId")]
        public int? EntryId { get; set; }

        [JsonPropertyName("labelName")]
        public string? LabelName { get; set; }
    }
}
