namespace FinanceManager.Api.Controllers.Admin;

public sealed record AiProviderDto(
    string ProviderName,
    string BaseUrl,
    bool HasApiKey,
    int RequestTimeoutSeconds,
    bool IsEnabled);

public sealed record AiFallbackEntryDto(string ProviderName, string Model, int Order);

public sealed record AiConfigurationResponse(
    IReadOnlyList<AiProviderDto> Providers,
    IReadOnlyList<AiFallbackEntryDto> FallbackEntries);

public sealed record UpdateProviderRequest(
    string BaseUrl,
    string? ApiKey,
    int RequestTimeoutSeconds,
    bool IsEnabled);

public sealed record UpdateFallbackRequest(
    IReadOnlyList<AiFallbackEntryDto> Entries);
