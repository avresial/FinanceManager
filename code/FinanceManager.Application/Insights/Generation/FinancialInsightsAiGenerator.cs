using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Insights.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Insights.Generation;

internal sealed class FinancialInsightsAiGenerator(
    InsightsContextBuilder insightsContextBuilder,
    IInsightsPromptProvider insightsPromptProvider,
    IChatClient chatClient,
    InsightsResponseParser insightsResponseParser,
    FinancialInsightNormalizer financialInsightNormalizer,
    ILogger<FinancialInsightsAiGenerator> logger) : IFinancialInsightsAiGenerator
{
    private const string _systemPrompt = "You are a finance assistant that outputs strict JSON.";
    private const string _modelId = "gpt-5-mini";

    public async Task<List<FinancialInsight>> GenerateInsights(int userId, int? accountId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        var entriesContextCsv = await insightsContextBuilder.BuildEntriesContextCsv(userId, accountId, cancellationToken);
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

            var parsed = insightsResponseParser.Parse(content);
            if (parsed.Count == 0)
                return [];

            return financialInsightNormalizer.Normalize(parsed, userId, accountId, count);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "AI insights generation cancelled for user {UserId}.", userId);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "AI insights generation cancelled or timed out for user {UserId}.", userId);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI insights generation failed");
            return [];
        }
    }
}