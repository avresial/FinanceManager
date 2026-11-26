using FinanceManager.Domain.Currencies.Services;

namespace FinanceManager.Application.Currencies.ExchangeRates;

public sealed record CurrencyExchangeRateResolution(
    CurrencyExchangeRateResult Result,
    CurrencyExchangeRateSource Source);