namespace FinanceManager.Components.Features.MoneyFlow.Models;

/// <summary>Expected activity of one calendar month, in date order.</summary>
public sealed record ForecastActivityMonth(DateTime Month, IReadOnlyList<ForecastActivityRow> Rows)
{
    public decimal Net => Rows.Sum(row => row.Transaction.Amount);
}