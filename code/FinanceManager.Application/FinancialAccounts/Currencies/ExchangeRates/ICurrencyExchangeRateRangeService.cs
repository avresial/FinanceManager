using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

/// <summary>Provides range results with enough provenance for safe point-cache promotion.</summary>
internal interface ICurrencyExchangeRateRangeService
{
    Task<List<(DateTime Date, decimal? Value, bool IsAuthoritative)>> GetExchangeRateRangeWithProvenanceAsync(
        Currency fromCurrency,
        Currency toCurrency,
        DateTime dateStart,
        DateTime dateEnd);
}