namespace FinanceManager.Application.Options;

public sealed class GitHubModelsOptions
{
    public string BaseUrl { get; set; } = "https://models.inference.ai.azure.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5-mini";
    public int RequestTimeoutSeconds { get; set; } = 180;
}
