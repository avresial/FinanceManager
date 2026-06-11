using FinanceManager.Domain.Entities.Users;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Insights.Generation;

internal sealed class FinancialInsightsAiGenerator(
    InsightsContextBuilder contextBuilder,
    IInsightsPromptProvider insightsPromptProvider,
    InsightsResponseParser responseParser,
    FinancialInsightNormalizer normalizer,
    IChatClient chatClient,
    ILogger<FinancialInsightsAiGenerator> logger) : IFinancialInsightsAiGenerator
{
    private const string _systemPrompt = "You are a finance assistant that outputs strict JSON.";
    private const string _modelId = "gpt-5-mini";

    public async Task<List<FinancialInsight>> GenerateInsights(int userId, int? accountId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        var entriesContextCsv = await contextBuilder.BuildEntriesContextCsv(userId, accountId, cancellationToken);
        var prompt = await insightsPromptProvider.BuildPromptAsync(entriesContextCsv, cancellationToken);

        try
        {
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
                return [];

            var parsed = responseParser.Parse(content);
            if (parsed.Count == 0)
                return [];

            return normalizer.Normalize(parsed, userId, accountId, count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI insights generation failed");
            return [];
        }
    }
}