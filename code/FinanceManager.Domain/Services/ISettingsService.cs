using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Services;

public interface ISettingsService
{
    Currency GetCurrency();
}
