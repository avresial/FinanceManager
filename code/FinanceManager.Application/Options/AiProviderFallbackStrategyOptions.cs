namespace FinanceManager.Application.Options;

public sealed class AiProviderFallbackStrategyOption
{
    public string Name { get; set; } = string.Empty;

    public List<AiProviderFallbackEntryOption> Providers { get; set; } = [];
}

public sealed class AiProviderFallbackEntryOption
{
    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}
