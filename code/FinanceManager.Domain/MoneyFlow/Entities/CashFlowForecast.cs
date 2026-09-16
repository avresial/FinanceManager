namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>
/// Read-only cash-flow projection for one user and reporting currency.
/// </summary>
public sealed class CashFlowForecast
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
    public int HorizonDays { get; set; }
    public bool HasForecastableActivity { get; set; }
    public List<TimeSeriesModel> HistoricalSeries { get; set; } = [];
    public List<TimeSeriesModel> ForecastSeries { get; set; } = [];
    public List<CashFlowForecastTransaction> ExpectedTransactions { get; set; } = [];
}