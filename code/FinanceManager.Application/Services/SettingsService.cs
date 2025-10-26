using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class SettingsService : ISettingsService
{
    public Currency GetCurrency() => DefaultCurrency.PLN;
}
