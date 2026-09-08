using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

internal sealed record CurrencyExchangeRateResolution(
    CurrencyExchangeRateResult Result,
    CurrencyExchangeRateSource Source);