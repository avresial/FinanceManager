using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Application.Labels.Suggestions;

internal static class LabelSuggestionResponseParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static IReadOnlyList<SuggestionItem> Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

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
                items = root.Suggestions.OfType<SuggestionItem>().ToList();
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
        public List<SuggestionItem?> Suggestions { get; set; } = [];
    }
}