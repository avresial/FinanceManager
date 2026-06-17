using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Identity.Users;

public class SettingsService : ISettingsService
{
    public Currency GetCurrency() => DefaultCurrency.PLN;
}