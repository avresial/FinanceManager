namespace FinanceManager.Domain.Shared.Ai.Entities;

public class AiProviderConfiguration
{
    public string ProviderName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 180;
    public bool IsEnabled { get; set; } = true;
}