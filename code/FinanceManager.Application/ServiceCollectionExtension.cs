using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<ISettingsService, SettingsService>()
                    .AddScoped<ILoginService, LoginService>()
                    .AddScoped<IMoneyFlowService, MoneyFlowService>()
                    .AddScoped<AccountDataSynchronizationService, AccountDataSynchronizationService>()
                    .AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
            return services;
        }
    }
}
