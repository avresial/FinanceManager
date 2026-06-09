namespace FinanceManager.Components.HttpClients;

public sealed record UpdateServiceKeyRequest(
    string BaseUrl,
    string? ApiKey,
    bool IsEnabled);