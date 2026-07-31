namespace FinanceManager.Application.Shared.Options;

/// <summary>
/// Configuration for the ECB (European Central Bank) Data Portal exchange-rate provider. The public
/// ECB Data Portal API is keyless and publishes official EUR reference rates, so this provider is
/// preferred for EUR pairs and supports cross rates between two currencies both quoted against EUR.
/// Set <see cref="Enabled"/> to <c>false</c> to remove it from the resolution chain without changing
/// DI wiring.
/// </summary>
public sealed class EcbOptions
{
    /// <summary>Configuration section name bound from appsettings.</summary>
    public const string SectionName = "Ecb";

    /// <summary>Base URL for the ECB Data Portal SDMX API. Defaults to the official endpoint.</summary>
    public string BaseUrl { get; set; } = "https://data-api.ecb.europa.eu/service";

    /// <summary>When <c>false</c>, the provider is skipped and reports every pair as not found.</summary>
    public bool Enabled { get; set; } = true;
}