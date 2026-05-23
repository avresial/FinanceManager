namespace FinanceManager.Components.HttpClients;

public sealed record AiConfigurationResponse(
    List<AiProviderDto> Providers,
    List<AiFallbackEntryDto> FallbackEntries,
    List<string> KnownProviders);
