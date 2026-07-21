using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.Identity.Services;

public interface ISettingsService
{
    /// <summary>Last resolved display currency; falls back to the app default until <see cref="GetCurrencyAsync"/> ran.</summary>
    Currency GetCurrency();

    /// <summary>Resolves the logged-in user's preferred currency, falling back to the app default.</summary>
    Task<Currency> GetCurrencyAsync();
}