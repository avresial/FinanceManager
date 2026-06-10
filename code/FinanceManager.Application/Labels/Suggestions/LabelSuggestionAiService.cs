using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Application.Labels.Suggestions;

internal sealed class LabelSuggestionAiService(
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntryRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    ILabelSuggestionPromptProvider promptProvider,
    IChatClient chatClient,
    ILogger<LabelSuggestionAiService> logger) : ILabelSuggestionAiService
{
    private const string _systemPrompt = "You are a finance assistant that proposes new transaction labels. Output strict JSON only.";
    private const string _modelId = "gpt-5-mini";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<LabelSuggestion>> SuggestLabels(
        int entrySampleSize = 100,
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        if (entrySampleSize <= 0 || maxSuggestions <= 0) return [];

        var entries = await currencyEntryRepository.GetRecentUnlabelled(entrySampleSize, cancellationToken);
        if (entries.Count == 0)
        {
            logger.LogInformation("No unlabelled entries available for label suggestion.");
            return [];
        }

        var existingLabels = await financialLabelsRepository.GetLabels(cancellationToken).ToListAsync(cancellationToken);
        var existingNames = string.Join(", ", existingLabels.Select(l => l.Name));
        var existingNameSet = new HashSet<string>(existingLabels.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);

        var csv = AiEntryCsvBuilder.BuildForLabelSuggestion(entries);
        var prompt = await promptProvider.BuildPromptAsync(existingNames, csv, maxSuggestions, cancellationToken);

        try
        {
            logger.LogDebug("Requesting label suggestions from {EntryCount} unlabelled entries.", entries.Count);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, _systemPrompt),
                new(ChatRole.User, prompt)
            };
            var chatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.Json,
                ModelId = _modelId
            };

            var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            var content = response.Text;
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("AI model returned empty response for label suggestions.");
                return [];
            }

            var parsed = TryParseSuggestions(content);
            logger.LogDebug("Parsed {Count} suggestions from AI response.", parsed.Count);

            var result = new List<LabelSuggestion>();
            foreach (var item in parsed)
            {
                var name = item.LabelName?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (existingNameSet.Contains(name)) continue;
                if (result.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))) continue;

                var examples = (item.ExampleDescriptions ?? [])
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();

                result.Add(new LabelSuggestion(
                    Name: name,
                    Rationale: (item.Rationale ?? string.Empty).Trim(),
                    ExampleDescriptions: examples));

                if (result.Count >= maxSuggestions) break;
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI label suggestion failed.");
            return [];
        }
    }

    private static List<SuggestionItem> TryParseSuggestions(string content)
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

        static bool TryDeserialize(string json, out List<SuggestionItem> items)
        {
            items = [];
            try
            {
                var root = JsonSerializer.Deserialize<SuggestionsRoot>(json, _jsonOptions);
                if (root?.Suggestions is null) return false;
                items = root.Suggestions;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private sealed class SuggestionsRoot
    {
        [JsonPropertyName("suggestions")]
        public List<SuggestionItem> Suggestions { get; set; } = [];
    }

    private sealed class SuggestionItem
    {
        [JsonPropertyName("labelName")]
        public string? LabelName { get; set; }

        [JsonPropertyName("rationale")]
        public string? Rationale { get; set; }

        [JsonPropertyName("exampleDescriptions")]
        public List<string>? ExampleDescriptions { get; set; }
    }
}