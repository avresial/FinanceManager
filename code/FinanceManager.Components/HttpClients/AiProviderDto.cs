namespace FinanceManager.Components.HttpClients;

public sealed record AiProviderDto(
    string ProviderName,
    string BaseUrl,
    bool HasApiKey,
    int RequestTimeoutSeconds,
    bool IsEnabled,
    List<AiProviderModelDto> Models);
