using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Components.Models;

public sealed class InvestmentPaycheckEstimateCacheSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal WithdrawalRate { get; set; }
    public int SalaryMonths { get; set; }
    public DateTime FetchedAtUtc { get; set; }
    public InvestmentPaycheckEstimate Estimate { get; set; } = new();
}

public sealed class InvestmentPaycheckEstimateRefreshContext
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal WithdrawalRate { get; set; }
    public int SalaryMonths { get; set; }
}