using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Application.Labels.Setter;

internal sealed class LabelAssignmentResponseParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<LabelAssignment> Parse(string content)
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

        static bool TryDeserialize(string json, out List<LabelAssignment> items)
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
        public List<LabelAssignment> Assignments { get; set; } = [];
    }
}