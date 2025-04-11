using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Providers;
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
                    .AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
            return services;
        }

        public static IServiceCollection AddApplicationApi(this IServiceCollection services)
        {
            services.AddScoped<ISettingsService, SettingsService>()
                    .AddScoped<IMoneyFlowService, MoneyFlowService>()
                    .AddScoped<AccountIdProvider>()
                    .AddScoped<GuestAccountSeeder>()
                ;

            return services;
        }
    }
}
