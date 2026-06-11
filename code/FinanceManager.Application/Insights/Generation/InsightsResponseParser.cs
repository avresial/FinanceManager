using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Application.Insights.Generation;

internal sealed class InsightsResponseParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<InsightItem> Parse(string content)
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

        static bool TryDeserialize(string json, out List<InsightItem> items)
        {
            items = [];
            try
            {
                var root = JsonSerializer.Deserialize<InsightsRoot>(json, _jsonOptions);
                if (root?.Insights is null) return false;
                items = root.Insights;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private sealed class InsightsRoot
    {
        [JsonPropertyName("insights")]
        public List<InsightItem> Insights { get; set; } = [];
    }
}