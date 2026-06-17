namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>
/// Represents an inflation rate for a specific currency on a given date.
/// </summary>
public record InflationRate(int CurrencyId, DateOnly Date, decimal Rate);