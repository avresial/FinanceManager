using FinanceManager.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OpenRouterProvider(
    HttpClient httpClient,
    IOptions<OpenRouterOptions> options,
    ILogger<OpenRouterProvider> logger)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string?> Get(string prompt, CancellationToken cancellationToken = default)
    {
        var model = options.Value.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            logger.LogWarning("OpenRouter model is not configured.");
            return null;
        }

        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("OpenRouter API key is not configured.");
            return null;
        }

        var baseUrl = options.Value.BaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var normalizedBaseUrl = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
            httpClient.BaseAddress = new Uri(normalizedBaseUrl, UriKind.Absolute);
        }

        var request = new OpenRouterChatRequest
        {
            Model = model,
            Messages =
            [
                new OpenRouterMessage("system", "You are a finance assistant that outputs strict JSON."),
                new OpenRouterMessage("user", prompt)
            ],
            ResponseFormat = new OpenRouterResponseFormat { Type = "json_object" }
        };

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(request)
            };

            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenRouter chat request failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var extractedContent = ExtractContent(rawContent);

            if (string.IsNullOrWhiteSpace(extractedContent))
            {
                logger.LogWarning("OpenRouter returned success but no extractable content. Payload snippet: {PayloadSnippet}",
                    rawContent.Length <= 500 ? rawContent : rawContent[..500]);
            }

            return extractedContent;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OpenRouter request timed out");
            return null;
        }
    }

    private static string? ExtractContent(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
            return null;

        try
        {
            var typed = JsonSerializer.Deserialize<OpenRouterChatResponse>(rawContent, _jsonOptions);
            var direct = typed?.Choices?.FirstOrDefault()?.Message?.Content;
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;
        }
        catch
        {
            // fall back to JsonDocument parsing below
        }

        try
        {
            using var doc = JsonDocument.Parse(rawContent);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                return null;

            var firstChoice = choices[0];

            if (firstChoice.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("content", out var contentElement))
                {
                    if (contentElement.ValueKind == JsonValueKind.String)
                        return contentElement.GetString();

                    if (contentElement.ValueKind == JsonValueKind.Array)
                    {
                        var parts = new List<string>();
                        foreach (var part in contentElement.EnumerateArray())
                        {
                            if (part.ValueKind == JsonValueKind.String)
                            {
                                var partText = part.GetString();
                                if (!string.IsNullOrWhiteSpace(partText)) parts.Add(partText);
                                continue;
                            }

                            if (part.ValueKind == JsonValueKind.Object)
                            {
                                if (part.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                                {
                                    var partText = textProp.GetString();
                                    if (!string.IsNullOrWhiteSpace(partText)) parts.Add(partText);
                                    continue;
                                }

                                if (part.TryGetProperty("content", out var nestedContent) && nestedContent.ValueKind == JsonValueKind.String)
                                {
                                    var partText = nestedContent.GetString();
                                    if (!string.IsNullOrWhiteSpace(partText)) parts.Add(partText);
                                }
                            }
                        }

                        if (parts.Count != 0)
                            return string.Join("\n", parts);
                    }
                }
            }

            if (firstChoice.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                return textElement.GetString();
        }
        catch
        {
            return null;
        }

        return null;
    }

    private sealed class OpenRouterChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenRouterMessage> Messages { get; set; } = [];

        [JsonPropertyName("response_format")]
        public OpenRouterResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class OpenRouterResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    private sealed record OpenRouterMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class OpenRouterChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice>? Choices { get; set; }
    }

    private sealed class OpenRouterChoice
    {
        [JsonPropertyName("message")]
        public OpenRouterMessage? Message { get; set; }
    }
}