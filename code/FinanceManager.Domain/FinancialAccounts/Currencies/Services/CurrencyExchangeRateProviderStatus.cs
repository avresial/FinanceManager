namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

public enum CurrencyExchangeRateProviderStatus
{
    Success,
    NotFound,
    OutOfRange,
    Failed,
}