namespace FinanceManager.Application.Options;

/// <summary>
/// Configuration for OpenFIGI API client.
/// OpenFIGI (www.openfigi.com) is a service that maps ticker symbols to ISINs.
/// </summary>
public sealed class OpenFigiOptions
{
    /// <summary>
    /// Base URL for OpenFIGI API. Defaults to the public endpoint.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openfigi.com/v3";

    /// <summary>
    /// Optional API key for higher rate limits.
    /// Without a key, the API allows 25 requests per minute.
    /// With a key, it allows 250 requests per minute.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}