namespace FinanceManager.Application.Options;

public sealed class StockApiOptions
{
    public string BaseUrl { get; set; } = "https://www.alphavantage.co/query";
    public string ApiKey { get; set; } = string.Empty;
    public string OutputSize { get; set; } = "compact";
}