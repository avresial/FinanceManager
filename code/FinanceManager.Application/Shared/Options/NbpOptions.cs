namespace FinanceManager.Application.Shared.Options;

/// <summary>
/// Configuration for the NBP (Narodowy Bank Polski) currency exchange-rate provider. The public NBP
/// API is keyless and quotes official PLN-based rates from table A, so this provider is preferred for
/// PLN pairs. Set <see cref="Enabled"/> to <c>false</c> to remove it from the resolution chain without
/// changing DI wiring.
/// </summary>
public sealed class NbpOptions
{
    /// <summary>Configuration section name bound from appsettings.</summary>
    public const string SectionName = "Nbp";

    /// <summary>Base URL for the NBP public API. Defaults to the official endpoint.</summary>
    public string BaseUrl { get; set; } = "https://api.nbp.pl/api";

    /// <summary>When <c>false</c>, the provider is skipped and reports every pair as not found.</summary>
    public bool Enabled { get; set; } = true;
}