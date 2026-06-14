namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Resolves a search term (broker ticker, symbol, or name) to an instrument.
/// Returns either a single auto-resolved match or a list of candidates for user confirmation.
/// </summary>
public interface IInstrumentResolver
{
    /// <summary>
    /// Resolve an instrument search term.
    /// </summary>
    /// <param name="brokerTickerOrTerm">A broker ticker (e.g., "CSPX.UK"), base ticker, or search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Resolution result with Kind and either Match or Candidates.</returns>
    Task<InstrumentResolution> ResolveAsync(string brokerTickerOrTerm, CancellationToken ct = default);
}