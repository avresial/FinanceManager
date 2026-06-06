namespace FinanceManager.Components.HttpClients;

public sealed record ExternalServiceDto(
    string ServiceName,
    string DisplayName,
    string Description,
    string DocsUrl,
    string BaseUrl,
    bool HasApiKey,
    bool IsEnabled);