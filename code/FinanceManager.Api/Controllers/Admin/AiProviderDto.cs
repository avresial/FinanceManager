namespace FinanceManager.Api.Controllers.Admin;

public sealed record AiProviderDto(
    string ProviderName,
    string BaseUrl,
    bool HasApiKey,
    int RequestTimeoutSeconds,
    bool IsEnabled,
    IReadOnlyList<AiProviderModelDto> Models);