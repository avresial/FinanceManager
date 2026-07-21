using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Administration;

public sealed record AddModelRequest([Required, StringLength(256)] string ModelName);

public sealed record AddProviderRequest([Required, StringLength(256)] string ProviderName);

public sealed record AiConfigurationResponse(
    IReadOnlyList<AiProviderDto> Providers,
    IReadOnlyList<AiFallbackEntryDto> FallbackEntries,
    IReadOnlyList<string> KnownProviders);

public sealed record AiFallbackEntryDto(
    [Required, StringLength(256)] string ProviderName,
    [Required, StringLength(256)] string Model,
    [Range(0, int.MaxValue)] int Order);

public sealed record AiProviderDto(
    string ProviderName,
    string BaseUrl,
    bool HasApiKey,
    int RequestTimeoutSeconds,
    bool IsEnabled,
    IReadOnlyList<AiProviderModelDto> Models);

public sealed record AiProviderModelDto(int Id, string ModelName, bool IsEnabled);

public sealed record ExternalServiceDto(
    string ServiceName,
    string DisplayName,
    string Description,
    string DocsUrl,
    string BaseUrl,
    bool HasApiKey,
    bool IsEnabled);

public sealed record ExternalServicesResponse(IReadOnlyList<ExternalServiceDto> Services);

public sealed record GeneratedMaintenanceKeyResponse(string Key, DateTimeOffset CreatedAt);

public sealed record MaintenanceKeyStatusResponse(bool HasKey, DateTimeOffset? CreatedAt);

public sealed record UpdateFallbackRequest(
    [Required, MinLength(1)] IReadOnlyList<AiFallbackEntryDto> Entries);

public sealed record UpdateModelRequest(
    [Required, StringLength(256)] string ModelName,
    bool IsEnabled);

public sealed record UpdateProviderRequest(
    [Required, Url, StringLength(2048)] string BaseUrl,
    [StringLength(4096)] string? ApiKey,
    [Range(1, 600)] int RequestTimeoutSeconds,
    bool IsEnabled);

public sealed record UpdateServiceKeyRequest(
    [Required, Url, StringLength(2048)] string BaseUrl,
    [StringLength(4096)] string? ApiKey,
    bool IsEnabled);