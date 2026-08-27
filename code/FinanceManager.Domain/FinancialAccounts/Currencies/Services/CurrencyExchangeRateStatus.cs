namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

/// <summary>Terminal state of an exchange-rate resolution.</summary>
public enum CurrencyExchangeRateStatus
{
    Success,
    NotFound,
    NotYetPublished,
    Failed,
}