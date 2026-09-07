namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

/// <summary>Describes how a range value was obtained, independently of cache policy.</summary>
internal enum CurrencyExchangeRateSource
{
    Unknown,
    Stored,
    Provider,
    SameCurrency,
    DerivedViaUsd,
    CarriedForward,
    Unavailable
}