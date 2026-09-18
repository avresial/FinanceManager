using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>
/// One expected recurring cash movement in a forecast horizon. Positive amounts are inflows;
/// negative amounts are outflows.
/// </summary>
public sealed class CashFlowForecastTransaction
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurringCadence Cadence { get; set; }
    public Guid PatternId { get; set; }
}