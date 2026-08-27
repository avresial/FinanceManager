namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

public enum CurrencyExchangeRateProviderStatus
{
    Success,
    NotFound,
    /// <summary>The requested current UTC date has not been published by the provider yet.</summary>
    NotYetPublished,
    OutOfRange,
    Failed,
}