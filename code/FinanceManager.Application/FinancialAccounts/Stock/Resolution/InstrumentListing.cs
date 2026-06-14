namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Reconciled candidate instrument listing after cross-referencing Alpha Vantage and OpenFIGI data.
/// Represents a single venue or entry for an instrument.
/// </summary>
public sealed record InstrumentListing(
    string? Isin,
    string? AlphaVantageSymbol,
    string Name,
    string Exchange,
    string Currency,
    string Type,
    decimal QuoteFactor = 1m);