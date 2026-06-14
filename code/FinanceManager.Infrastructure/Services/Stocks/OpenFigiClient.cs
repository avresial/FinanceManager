using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Services.Stocks;

/// <summary>
/// OpenFIGI API client for resolving ticker symbols to ISINs and listing metadata.
/// See: https://www.openfigi.com/api
/// </summary>
internal sealed class OpenFigiClient(
    HttpClient httpClient,
    ILogger<OpenFigiClient> logger,
    IExternalServiceConfigService configService) : IOpenFigiClient, IIsinResolver
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<OpenFigiListing>> MapByTickerAsync(string baseTicker, string? exchCode = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseTicker))
            return [];

        var request = new OpenFigiMappingRequest
        {
            IdType = "TICKER",
            IdValue = baseTicker,
            ExchCode = exchCode ?? string.Empty
        };

        return await ExecuteMapping([request], $"ticker {baseTicker} / exchCode {exchCode ?? "(any)"}", ct);
    }

    public async Task<IReadOnlyList<OpenFigiListing>> MapByIsinAsync(string isin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isin))
            return [];

        var request = new OpenFigiMappingRequest
        {
            IdType = "ID_ISIN",
            IdValue = isin,
            ExchCode = string.Empty
        };

        return await ExecuteMapping([request], $"ISIN {isin}", ct);
    }

    public async Task<string?> ResolveAsync(string ticker, string? region = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;

        var results = await MapByTickerAsync(ticker, region, ct);
        var first = results.FirstOrDefault();
        if (first is null)
        {
            logger.LogDebug("OpenFIGI returned no results for ticker {Ticker}", ticker);
            return null;
        }

        if (string.IsNullOrWhiteSpace(first.Isin))
        {
            logger.LogDebug("OpenFIGI returned no ISIN for ticker {Ticker}", ticker);
            return null;
        }

        logger.LogDebug("Resolved ticker {Ticker} to ISIN {Isin}", ticker, first.Isin);
        return first.Isin;
    }

    private async Task<IReadOnlyList<OpenFigiListing>> ExecuteMapping(List<OpenFigiMappingRequest> requests, string debugLabel, CancellationToken ct)
    {
        var serviceConfig = await configService.GetServiceAsync("OpenFigi", ct);
        var url = $"{serviceConfig.BaseUrl.TrimEnd('/')}/mapping";

        try
        {
            var content = JsonSerializer.Serialize(requests);
            var httpContent = new StringContent(content, System.Text.Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(serviceConfig.ApiKey))
                httpContent.Headers.Add("X-OPENFIGI-APIKEY", serviceConfig.ApiKey);

            var response = await httpClient.PostAsync(url, httpContent, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenFIGI mapping request failed with status {StatusCode} for {DebugLabel}", response.StatusCode, debugLabel);
                return [];
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var results = JsonSerializer.Deserialize<List<OpenFigiMappingResponse>>(responseContent, _jsonOptions);

            if (results is null || results.Count == 0)
            {
                logger.LogDebug("OpenFIGI returned no results for {DebugLabel}", debugLabel);
                return [];
            }

            var listings = new List<OpenFigiListing>(results.Count);
            foreach (var result in results)
            {
                listings.Add(new OpenFigiListing(
                    Isin: result.Isin,
                    Ticker: result.Ticker ?? string.Empty,
                    Name: result.Name ?? string.Empty,
                    ExchCode: result.ExchCode ?? string.Empty,
                    Currency: result.Currency));
            }

            return listings;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenFIGI mapping request failed for {DebugLabel}", debugLabel);
            return [];
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

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("exchCode")]
        public string? ExchCode { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }
    }
}