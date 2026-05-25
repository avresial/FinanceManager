namespace FinanceManager.Api.Services;

public class JwtAuthOptions
{
    public int TokenValidityMins { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}