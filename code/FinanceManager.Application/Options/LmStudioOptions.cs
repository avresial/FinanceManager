namespace FinanceManager.Application.Options;

public sealed class LmStudioOptions
{
    public string BaseUrl { get; set; } = "http://localhost:1234/v1";
    public string ApiKey { get; set; } = "lm-studio";
    public int RequestTimeoutSeconds { get; set; } = 180;
}