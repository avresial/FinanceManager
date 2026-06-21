namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// Describes the freshness/source of a <see cref="PriceQuote"/>.
/// </summary>
public enum PriceQuoteType
{
    RealTime,
    Delayed,
    EndOfDay,
    Manual
}