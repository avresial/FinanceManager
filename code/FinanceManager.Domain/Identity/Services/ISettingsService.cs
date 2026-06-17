using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.Identity.Services;

public interface ISettingsService
{
    Currency GetCurrency();
}