namespace FinanceManager.Domain.Currencies.Services;

public readonly record struct CurrencyExchangeRateProviderResult(
    CurrencyExchangeRateProviderStatus Status,
    decimal? Value = null,
    DateTime? RetryAtUtc = null);