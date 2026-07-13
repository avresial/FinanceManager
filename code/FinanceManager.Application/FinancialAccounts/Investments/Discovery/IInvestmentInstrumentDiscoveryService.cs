using FinanceManager.Domain.Assets.Discovery;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

/// <summary>
/// Higher-level, user-facing instrument discovery over the external providers (OpenFIGI and
/// Alpha Vantage). Normalizes and merges provider responses into listing-level results that the
/// investment transaction form and the admin asset page can present.
/// </summary>
public interface IInvestmentInstrumentDiscoveryService
{
    /// <summary>
    /// Search for investment instruments by ticker, name, or ISIN. Returns listing-level results
    /// (the same asset can appear once per exchange/currency), preferring OpenFIGI for identity and
    /// Alpha Vantage for the provider price symbol.
    /// </summary>
    Task<IReadOnlyList<InstrumentDiscoveryResultDto>> SearchAsync(string query, CancellationToken ct = default);
}