using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services
{
    public class SettingsService : ISettingsService
    {
        public string GetCurrency() => "PLN";
    }
}
