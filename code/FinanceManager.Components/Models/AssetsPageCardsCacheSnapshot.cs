using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Components.Models;

public sealed class AssetsPageCardsCacheSnapshot
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public DateTime FetchedAtUtc { get; set; }

    public List<TimeSeriesModel> AssetsTimeSeries { get; set; } = [];
    public List<NameValueResult> EndAssetsPerType { get; set; } = [];
    public List<NameValueResult> EndAssetsPerAccount { get; set; } = [];
    public List<InvestmentRate> InvestmentRates { get; set; } = [];
    public List<InvestmentRate> MonthlyInvestmentRates { get; set; } = [];
}

public sealed class AssetsPageCardsRefreshContext
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
}