using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

internal sealed record OllamaMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }
}