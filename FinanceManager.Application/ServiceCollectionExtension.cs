using FinanceManager.Application.Services;
using FinanceManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application
{
	public static class ServiceCollectionExtension
	{
		public static IServiceCollection AddApplication(this IServiceCollection services)
		{
			services.AddScoped<IAccountService, AccountService>()
					.AddScoped<ISettingsService, SettingsService>();

			return services;
		}
	}
}
