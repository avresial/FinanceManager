namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateServiceKeyRequest(
    string BaseUrl,
    string? ApiKey,
    bool IsEnabled);