using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Models;

public sealed class InvestmentRateCacheSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime EndDateTime { get; set; }
    public DateTime FetchedAtUtc { get; set; }
    public List<InvestmentRate> MonthlyInvestmentRates { get; set; } = [];
}

public sealed class InvestmentRateRefreshContext
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime EndDateTime { get; set; }
}