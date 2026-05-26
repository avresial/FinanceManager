namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateProviderRequest(
    string BaseUrl,
    string? ApiKey,
    int RequestTimeoutSeconds,
    bool IsEnabled);