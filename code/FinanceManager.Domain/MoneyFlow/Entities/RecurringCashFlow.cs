using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>
/// Signed recurring cash movement used by the forecast. Expense patterns retain subscription
/// state so muted or cancelled patterns can be excluded without changing the subscriptions view.
/// </summary>
public sealed class RecurringCashFlow
{
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyAmount { get; set; }
    public decimal OccurrenceAmount { get; set; }
    public RecurringCadence Cadence { get; set; }
    public DateTime NextExpectedDate { get; set; }
    public Guid PatternId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsCancelled { get; set; }
}