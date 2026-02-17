using FinanceManager.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OllamaProvider(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaProvider> logger)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<OllamaChatResponse?> Get(OllamaChatRequest request, CancellationToken cancellationToken = default)
    {
        var model = !string.IsNullOrWhiteSpace(request.Model)
            ? request.Model
            : options.Value.Model;

        if (string.IsNullOrWhiteSpace(model))
        {
            logger.LogWarning("Ollama model is not configured.");
            return null;
        }

        request.Model = model;

        var baseUrl = options.Value.BaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var normalizedBaseUrl = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
            httpClient.BaseAddress = new Uri(normalizedBaseUrl, UriKind.Absolute);
        }

        using var response = await httpClient.PostAsJsonAsync("api/chat", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Ollama chat request failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OllamaChatResponse>(_jsonOptions, cancellationToken);
    }
}