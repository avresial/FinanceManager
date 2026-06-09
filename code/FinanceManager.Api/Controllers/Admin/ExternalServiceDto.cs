namespace FinanceManager.Api.Controllers.Admin;

public sealed record ExternalServiceDto(
    string ServiceName,
    string DisplayName,
    string Description,
    string DocsUrl,
    string BaseUrl,
    bool HasApiKey,
    bool IsEnabled);