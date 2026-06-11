using FinanceManager.Domain.Entities.Users;

namespace FinanceManager.Application.Insights.Generation;

internal sealed class FinancialInsightNormalizer
{
    public List<FinancialInsight> Normalize(IReadOnlyList<InsightItem> parsed, int userId, int? accountId, int count)
    {
        var now = DateTime.UtcNow;
        var result = new List<FinancialInsight>(Math.Min(count, parsed.Count));

        foreach (var item in parsed.Take(count))
        {
            var title = Truncate(item.Title?.Trim() ?? string.Empty, 128);
            var message = Truncate(item.Message?.Trim() ?? string.Empty, 1024);
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
                continue;

            var tags = NormalizeTags(item.Tags);

            result.Add(new FinancialInsight
            {
                UserId = userId,
                AccountId = accountId,
                Title = title,
                Message = message,
                Tags = Truncate(string.Join(',', tags), 256),
                CreatedAt = now
            });
        }

        return result;
    }

    private static IReadOnlyList<string> NormalizeTags(List<string>? tags)
    {
        if (tags is null || tags.Count == 0)
            return ["summary"];

        var cleaned = tags
            .Select(t => (t ?? string.Empty).Trim().ToLowerInvariant())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(3)
            .ToList();

        return cleaned.Count == 0 ? ["summary"] : cleaned;
    }

    private static string Truncate(string value, int maxLen) =>
        value.Length <= maxLen ? value : value[..maxLen];
}