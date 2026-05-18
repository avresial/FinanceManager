namespace FinanceManager.Components.HttpClients;

public sealed record UpdateProviderRequest(
    string BaseUrl,
    string? ApiKey,
    int RequestTimeoutSeconds,
    bool IsEnabled);
