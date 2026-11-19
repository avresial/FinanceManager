using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Application.Labels.Suggestions;

internal static class LabelSuggestionResponseParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static List<SuggestionItem> Parse(string content)
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

    internal sealed class SuggestionItem
    {
        [JsonPropertyName("labelName")]
        public string? LabelName { get; set; }

        [JsonPropertyName("rationale")]
        public string? Rationale { get; set; }

        [JsonPropertyName("exampleDescriptions")]
        public List<string>? ExampleDescriptions { get; set; }
    }
}