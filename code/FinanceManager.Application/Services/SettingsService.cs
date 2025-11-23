using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class SettingsService : ISettingsService
{
    public Currency GetCurrency() => DefaultCurrency.PLN;
}