namespace FinanceManager.Domain.Shared.ExternalServices.Entities;

public class ExternalServiceConfiguration
{
    public string ServiceName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}