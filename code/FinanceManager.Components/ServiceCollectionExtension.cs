using FinanceManager.Components.HttpContexts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Components;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddUIComponents(this IServiceCollection services)
    {
        services.AddScoped<ILoginService, LoginService>()
                .AddScoped<StockPriceHttpContext>()
                .AddScoped<BankAccountHttpContext>()
                .AddScoped<StockAccountHttpContext>()
                .AddScoped<MoneyFlowHttpContext>()
                .AddScoped<AssetsHttpContext>()
                .AddScoped<LiabilitiesHttpContext>()
                .AddScoped<UserHttpContext>()
                .AddScoped<FinancialLabelHttpContext>()
                .AddScoped<AdministrationUsersHttpContext>()
                .AddScoped<NewVisitorsHttpContext>()
                .AddScoped<AccountDataSynchronizationService>()
                .AddScoped<IUserService, UserService>()
                .AddScoped<IFinancialAccountService, FinancialAccountService>()
                .AddScoped<DuplicateEntryResolverService>()
                ;
        return services;
    }
}
