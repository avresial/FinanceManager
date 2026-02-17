namespace FinanceManager.Application.Options;

public sealed class OpenRouterOptions
{
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "z-ai/glm-4.5-air:free";
    public int RequestTimeoutSeconds { get; set; } = 180;
}