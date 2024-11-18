using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IAccountService, AccountService>()
                    .AddScoped<ISettingsService, SettingsService>()
                    .AddScoped<ILoginService, LoginService>()
                    .AddScoped<IMoneyFlowService, MoneyFlowService>()
                    .AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
            return services;
        }
    }
}
