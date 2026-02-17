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

            var payload = await response.Content.ReadFromJsonAsync<OpenRouterChatResponse>(_jsonOptions, cancellationToken);
            return payload?.Choices?.FirstOrDefault()?.Message?.Content;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OpenRouter request timed out");
            return null;
        }
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
