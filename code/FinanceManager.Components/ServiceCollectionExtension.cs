using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Components
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddUIComponents(this IServiceCollection services)
        {
            services.AddScoped<ILoginService, LoginService>()
                    .AddScoped<AccountDataSynchronizationService>()
                    .AddScoped<IUserService, UserService>()
                    .AddScoped<BankAccountService>()
                    .AddScoped<IFinancalAccountService, FinancalAccountService>()
                    .AddScoped<IMoneyFlowService, MoneyFlowService>()
                    ;


            return services;
        }
    }
}
