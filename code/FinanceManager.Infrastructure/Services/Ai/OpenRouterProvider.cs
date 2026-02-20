using FinanceManager.Application.Options;
using FinanceManager.Application.Services.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OpenRouterProvider(
    HttpClient httpClient,
    IOptions<OpenRouterOptions> options,
    ILogger<OpenRouterProvider> logger) : IAiProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string?> Get(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
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
        Uri requestUri;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var normalizedBaseUrl = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
            var baseUri = new Uri(normalizedBaseUrl, UriKind.Absolute);
            requestUri = new Uri(baseUri, "chat/completions");
        }
        else if (httpClient.BaseAddress is not null)
        {
            requestUri = new Uri(httpClient.BaseAddress, "chat/completions");
        }
        else
        {
            requestUri = new Uri("chat/completions", UriKind.Relative);
        }

        var request = new OpenRouterChatRequest
        {
            Model = model,
            Messages =
            [
                new OpenRouterMessage("system", systemPrompt),
                new OpenRouterMessage("user", userPrompt)
            ],
            ResponseFormat = new OpenRouterResponseFormat { Type = "json_object" }
        };

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(request)
            };

            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "<none>";
            var contentLength = response.Content.Headers.ContentLength?.ToString() ?? "<unknown>";

            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorInfo = TryExtractError(rawContent);
                var friendlyMessage = GetFriendlyErrorMessage(response.StatusCode, errorInfo?.Code);

                logger.LogWarning(
                    "OpenRouter chat request failed. {Message} Status: {StatusCode}. Content-Type: {ContentType}, Content-Length: {ContentLength}.",
                    friendlyMessage,
                    response.StatusCode,
                    contentType,
                    contentLength);

                if (errorInfo is not null)
                {
                    logger.LogWarning(
                        "OpenRouter error details: Code={Code}, Provider={Provider}, IsByok={IsByok}, Raw={Raw}",
                        errorInfo.Code?.ToString() ?? "<none>",
                        string.IsNullOrWhiteSpace(errorInfo.ProviderName) ? "<unknown>" : errorInfo.ProviderName,
                        errorInfo.IsByok?.ToString() ?? "<unknown>",
                        Truncate(errorInfo.Raw, 300));
                }

                return null;
            }
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                logger.LogWarning(
                    "OpenRouter returned empty body with status {StatusCode}. Content-Type: {ContentType}, Content-Length: {ContentLength}",
                    response.StatusCode,
                    contentType,
                    contentLength);

                if (response.Headers.TryGetValues("x-openrouter-error", out var errorValues))
                {
                    var error = string.Join("; ", errorValues);
                    if (!string.IsNullOrWhiteSpace(error))
                        logger.LogWarning("OpenRouter error header: {Error}", error);
                }

                return null;
            }

            logger.LogWarning("{rawContent}", rawContent);
            var extractedContent = ExtractContent(rawContent);

            if (string.IsNullOrWhiteSpace(extractedContent))
            {
                logger.LogWarning(
                    "OpenRouter returned success but no extractable content. Content-Type: {ContentType}. Payload snippet: {PayloadSnippet}",
                    contentType,
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

    private static OpenRouterErrorInfo? TryExtractError(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
            return null;

        try
        {
            var typed = JsonSerializer.Deserialize<OpenRouterErrorResponse>(rawContent, _jsonOptions);
            var error = typed?.Error;
            if (error is null)
                return null;

            return new OpenRouterErrorInfo
            {
                Code = error.Code,
                Message = error.Message,
                ProviderName = error.Metadata?.ProviderName,
                IsByok = error.Metadata?.IsByok,
                Raw = error.Metadata?.Raw
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetFriendlyErrorMessage(HttpStatusCode statusCode, int? errorCode)
    {
        var code = errorCode ?? (int)statusCode;
        return code switch
        {
            400 => "Bad request - check payload or model name.",
            401 => "Unauthorized - check API key.",
            402 => "Payment required - check credits or billing.",
            403 => "Forbidden - key lacks access to the model.",
            404 => "Not found - check endpoint or model name.",
            408 => "Request timeout - try again.",
            409 => "Conflict - request could not be completed.",
            413 => "Payload too large - reduce prompt size.",
            415 => "Unsupported media type - check request content type.",
            422 => "Unprocessable - validation error in request.",
            429 => "Rate limited - slow down or retry later.",
            500 => "Server error - try again later.",
            502 => "Bad gateway - upstream provider issue.",
            503 => "Service unavailable - try again later.",
            504 => "Gateway timeout - upstream provider slow.",
            524 => "Upstream timeout - provider did not respond in time.",
            _ => "Request failed - see error details."
        };
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<none>";

        if (value.Length <= maxLength)
            return value;

        return value[..maxLength];
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

    private sealed class OpenRouterErrorResponse
    {
        [JsonPropertyName("error")]
        public OpenRouterError? Error { get; set; }
    }

    private sealed class OpenRouterError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("metadata")]
        public OpenRouterErrorMetadata? Metadata { get; set; }
    }

    private sealed class OpenRouterErrorMetadata
    {
        [JsonPropertyName("raw")]
        public string? Raw { get; set; }

        [JsonPropertyName("provider_name")]
        public string? ProviderName { get; set; }

        [JsonPropertyName("is_byok")]
        public bool? IsByok { get; set; }
    }

    private sealed class OpenRouterErrorInfo
    {
        public int? Code { get; set; }
        public string? Message { get; set; }
        public string? ProviderName { get; set; }
        public bool? IsByok { get; set; }
        public string? Raw { get; set; }
    }
}