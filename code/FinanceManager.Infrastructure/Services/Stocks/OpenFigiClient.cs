using FinanceManager.Application.Services.ExternalServices;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Services.Stocks;

/// <summary>
/// OpenFIGI API client for resolving ticker symbols to ISINs.
/// See: https://www.openfigi.com/api
/// </summary>
internal sealed class OpenFigiClient(
    HttpClient httpClient,
    ILogger<OpenFigiClient> logger,
    IExternalServiceConfigService configService) : IIsinResolver
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string?> ResolveAsync(string ticker, string? region = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;

        var serviceConfig = await configService.GetServiceAsync("OpenFigi", ct);
        var request = new OpenFigiMappingRequest
        {
            IdType = "TICKER",
            IdValue = ticker,
            ExchCode = region ?? string.Empty
        };

        var url = $"{serviceConfig.BaseUrl.TrimEnd('/')}/mapping";

        try
        {
            var content = JsonSerializer.Serialize(new[] { request });
            var httpContent = new StringContent(content, System.Text.Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(serviceConfig.ApiKey))
                httpContent.Headers.Add("X-OPENFIGI-APIKEY", serviceConfig.ApiKey);

            var response = await httpClient.PostAsync(url, httpContent, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenFIGI mapping request failed with status {StatusCode} for ticker {Ticker}", response.StatusCode, ticker);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var results = JsonSerializer.Deserialize<List<OpenFigiMappingResponse>>(responseContent, _jsonOptions);

            if (results is null || results.Count == 0)
            {
                logger.LogDebug("OpenFIGI returned no results for ticker {Ticker}", ticker);
                return null;
            }

            var isin = results[0].Isin;
            if (string.IsNullOrWhiteSpace(isin))
            {
                logger.LogDebug("OpenFIGI returned no ISIN for ticker {Ticker}", ticker);
                return null;
            }

            logger.LogDebug("Resolved ticker {Ticker} to ISIN {Isin}", ticker, isin);
            return isin;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenFIGI mapping request failed for ticker {Ticker}", ticker);
            return null;
        }
    }

    private sealed class OpenFigiMappingRequest
    {
        [JsonPropertyName("idType")]
        public string IdType { get; set; } = string.Empty;

        [JsonPropertyName("idValue")]
        public string IdValue { get; set; } = string.Empty;

        [JsonPropertyName("exchCode")]
        public string ExchCode { get; set; } = string.Empty;
    }

    private sealed class OpenFigiMappingResponse
    {
        [JsonPropertyName("figi")]
        public string? Figi { get; set; }

        [JsonPropertyName("ticker")]
        public string? Ticker { get; set; }

        [JsonPropertyName("isin")]
        public string? Isin { get; set; }

        [JsonPropertyName("compositeFigi")]
        public string? CompositeFigi { get; set; }
    }
}