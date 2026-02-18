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

internal sealed class OpenRouterLabelSetterAiService(
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntryRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    ILabelSetterPromptProvider promptProvider,
    IAccountCsvExportService<CurrencyAccountExportDto> csvExportService,
    OpenRouterProvider openRouterProvider,
    ILogger<OpenRouterLabelSetterAiService> logger) : ILabelSetterAiService
{
    private const int MaxEntriesPerBatch = 50;

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
            logger.LogInformation("No labels defined in the system – skipping label assignment.");
            return [];
        }

        var availableLabels = string.Join(", ", allLabels.Select(l => l.Name));
        var labelNameSet = new HashSet<string>(allLabels.Select(l => l.Name), StringComparer.Ordinal);

        var result = new Dictionary<int, string>();

        foreach (var batch in entryIds.Chunk(MaxEntriesPerBatch))
        {
            var entries = await currencyEntryRepository.GetByIds(batch, cancellationToken);
            if (entries.Count == 0) continue;

            var dtos = entries.Select(CurrencyAccountExportDto.FromEntity).ToList();
            var csv = csvExportService.GetExportResults(dtos);
            var prompt = await promptProvider.BuildPromptAsync(availableLabels, csv, cancellationToken);

            try
            {
                var content = await openRouterProvider.Get(prompt, cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    logger.LogWarning("OpenRouter returned empty response for label setter batch.");
                    continue;
                }

                var parsed = TryParseAssignments(content);
                foreach (var assignment in parsed)
                {
                    if (assignment.EntryId is null) continue;
                    if (string.IsNullOrWhiteSpace(assignment.LabelName)) continue;
                    if (!labelNameSet.Contains(assignment.LabelName)) continue;

                    result[assignment.EntryId.Value] = assignment.LabelName;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OpenRouter label setter failed for a batch of {Count} entries.", batch.Length);
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
