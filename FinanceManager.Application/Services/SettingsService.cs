using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
	internal class SettingsService : ISettingsService
	{
		public string GetCurrency() => "PLN";
	}
}
