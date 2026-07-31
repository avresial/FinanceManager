namespace FinanceManager.Application.Shared.Options;

public sealed class TwelveDataOptions
{
    public const string SectionName = "TwelveData";

    public string BaseUrl { get; set; } = "https://api.twelvedata.com";
    public string ApiKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 200;
    public int PerMinuteCreditLimit { get; set; } = 8;
    public int DailyCreditLimit { get; set; } = 800;
    public int RequestTimeoutSeconds { get; set; } = 30;
}