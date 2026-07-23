namespace FinanceManager.Application.Backfill.Currencies;

/// <summary>A normalized, upper-cased currency pair to backfill (<see cref="From"/> → <see cref="To"/>).</summary>
public readonly record struct CurrencyPair(string From, string To);