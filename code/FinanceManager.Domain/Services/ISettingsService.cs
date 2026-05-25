using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Services;

public interface ISettingsService
{
    Currency GetCurrency();
}