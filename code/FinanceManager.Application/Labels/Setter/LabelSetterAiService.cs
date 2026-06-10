using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Labels.Setter;

internal sealed class LabelSetterAiService(
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntryRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    ILabelSetterPromptProvider promptProvider,
    LabelAssignmentResponseParser responseParser,
    LabelAssignmentValidator assignmentValidator,
    IChatClient chatClient,
    ILogger<LabelSetterAiService> logger) : ILabelSetterAiService
{
    private const string _systemPrompt = "You are a finance assistant that outputs strict JSON.";
    private const string _modelId = "gpt-5-mini";

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

        logger.LogTrace("Retrieving {Count} entries for label assignment.", entryIds.Count);

        var entries = await currencyEntryRepository.GetByIds(entryIds, cancellationToken);
        if (entries.Count == 0)
        {
            logger.LogTrace("No entries found for {Count} entry IDs.", entryIds.Count);
            return [];
        }

        logger.LogTrace("Retrieved {Count} entries. Building CSV...", entries.Count);

        var csv = AiEntryCsvBuilder.BuildForCurrencyLabeling(entries);
        var prompt = await promptProvider.BuildPromptAsync(availableLabels, csv, cancellationToken);

        try
        {
            logger.LogDebug("Sending {Count} entries to AI model for label assignment.", entries.Count);

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
                logger.LogWarning("AI model returned empty response for label assignment batch.");
                return [];
            }

            var parsed = responseParser.Parse(content);
            logger.LogDebug("Parsed {Count} assignments from AI response.", parsed.Count);

            var result = assignmentValidator.Validate(parsed, entryIds, labelNameSet);

            logger.LogDebug("Valid assignments after filtering: {Count} out of {ParsedCount}.", result.Count, parsed.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI model label assignment failed for batch of {Count} entries.", entryIds.Count);
            return [];
        }
    }
}