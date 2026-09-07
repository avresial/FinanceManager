using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

internal sealed record CurrencyExchangeRateResolution(
    CurrencyExchangeRateResult Result,
    CurrencyExchangeRateSource Source)
{
    public bool CanReuseForRange =>
        Result.IsSuccess && Source is (CurrencyExchangeRateSource.Stored
            or CurrencyExchangeRateSource.Provider
            or CurrencyExchangeRateSource.SameCurrency);
}