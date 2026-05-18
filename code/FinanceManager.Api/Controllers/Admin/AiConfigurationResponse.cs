namespace FinanceManager.Api.Controllers.Admin;

public sealed record AiConfigurationResponse(
    IReadOnlyList<AiProviderDto> Providers,
    IReadOnlyList<AiFallbackEntryDto> FallbackEntries);
