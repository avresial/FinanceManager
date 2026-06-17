using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Identity.Services;
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

        return await ExecuteMapping([request], $"ticker {Sanitize(baseTicker)} / exchCode {Sanitize(exchCode) ?? "(any)"}", ct);
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

        return await ExecuteMapping([request], $"ISIN {Sanitize(isin)}", ct);
    }

    public async Task<string?> ResolveAsync(string ticker, string? region = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;

        try
        {
            var results = await MapByTickerAsync(ticker, region, ct);
            var first = results.FirstOrDefault();
            if (first is null)
            {
                logger.LogDebug("OpenFIGI returned no results for ticker {Ticker}", Sanitize(ticker));
                return null;
            }

            if (string.IsNullOrWhiteSpace(first.Isin))
            {
                logger.LogDebug("OpenFIGI returned no ISIN for ticker {Ticker}", Sanitize(ticker));
                return null;
            }

            logger.LogDebug("Resolved ticker {Ticker} to ISIN {Isin}", Sanitize(ticker), Sanitize(first.Isin));
            return first.Isin;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Legacy IIsinResolver contract returns null on failure; the decorating
            // CachingIsinResolver handles its own cooldown when null is returned.
            logger.LogWarning(ex, "OpenFIGI resolve failed for ticker {Ticker}", Sanitize(ticker));
            return null;
        }
    }

    private async Task<IReadOnlyList<OpenFigiListing>> ExecuteMapping(List<OpenFigiMappingRequest> requests, string debugLabel, CancellationToken ct)
    {
        var serviceConfig = await configService.GetServiceAsync("OpenFigi", ct);
        var url = $"{serviceConfig.BaseUrl.TrimEnd('/')}/mapping";

        var content = JsonSerializer.Serialize(requests);
        using var httpContent = new StringContent(content, System.Text.Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(serviceConfig.ApiKey))
            httpContent.Headers.Add("X-OPENFIGI-APIKEY", serviceConfig.ApiKey);

        using var response = await httpClient.PostAsync(url, httpContent, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Surface transport-level failures (rate limits, outages) so callers can back off /
            // enter cooldown. Distinct from a successful response that simply contains no listings.
            logger.LogWarning("OpenFIGI mapping request failed with status {StatusCode} for {DebugLabel}", response.StatusCode, debugLabel);
            throw new HttpRequestException($"OpenFIGI mapping request failed with status {response.StatusCode}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(ct);

        List<OpenFigiMappingResponse>? jobs;
        try
        {
            jobs = JsonSerializer.Deserialize<List<OpenFigiMappingResponse>>(responseContent, _jsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "OpenFIGI returned unparseable content for {DebugLabel}", debugLabel);
            return [];
        }

        if (jobs is null || jobs.Count == 0)
        {
            logger.LogDebug("OpenFIGI returned no results for {DebugLabel}", debugLabel);
            return [];
        }

        // OpenFIGI v3 wraps each job's matches inside a "data" array; flatten across all jobs.
        var listings = new List<OpenFigiListing>();
        foreach (var job in jobs)
        {
            if (job.Data is null)
                continue;

            foreach (var match in job.Data)
            {
                listings.Add(new OpenFigiListing(
                    Isin: match.Isin,
                    Ticker: match.Ticker ?? string.Empty,
                    Name: match.Name ?? string.Empty,
                    ExchCode: match.ExchCode ?? string.Empty,
                    Currency: match.Currency));
            }
        }

        if (listings.Count == 0)
            logger.LogDebug("OpenFIGI returned no listings for {DebugLabel}", debugLabel);

        return listings;
    }

    // Strip CR/LF so attacker-influenced ticker/ISIN values cannot forge log entries (log injection).
    private static string? Sanitize(string? value)
        => value?.Replace("\r", string.Empty).Replace("\n", string.Empty);

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
        [JsonPropertyName("data")]
        public List<OpenFigiMatch>? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private sealed class OpenFigiMatch
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