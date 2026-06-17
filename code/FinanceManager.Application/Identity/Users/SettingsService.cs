using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;

namespace FinanceManager.Application.Identity.Users;

public class SettingsService : ISettingsService
{
    public Currency GetCurrency() => DefaultCurrency.PLN;
}