using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Identity.Services;

public interface ISettingsService
{
    Currency GetCurrency();
}