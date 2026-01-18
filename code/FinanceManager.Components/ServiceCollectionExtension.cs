using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Components;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddUIComponents(this IServiceCollection services)
    {
        services.AddScoped<ILoginService, LoginService>()

                .AddScoped<StockPriceHttpClient>()
                .AddScoped<StockAccountHttpClient>()
                .AddScoped<StockEntryHttpClient>()

                .AddScoped<CurrencyAccountHttpClient>()
                .AddScoped<CurrencyAccountImportHttpClient>()
                .AddScoped<CurrencyEntryHttpClient>()

                .AddScoped<BondAccountHttpClient>()
                .AddScoped<BondEntryHttpClient>()
                .AddScoped<BondDetailsHttpClient>()

                .AddScoped<MoneyFlowHttpClient>()
                .AddScoped<AssetsHttpClient>()
                .AddScoped<LiabilitiesHttpClient>()
                .AddScoped<UserHttpClient>()
                .AddScoped<FinancialLabelHttpClient>()
                .AddScoped<AdministrationUsersHttpClient>()
                .AddScoped<NewVisitorsHttpClient>()
                .AddScoped<AccountDataSynchronizationService>()
                .AddScoped<IUserService, UserService>()
                .AddScoped<IFinancialAccountService, FinancialAccountService>()
                .AddScoped<IUserRepository, UserLocalStorageRepository>();
        ;

        return services;
    }
}