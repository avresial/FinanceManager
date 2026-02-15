using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Application.Services.Bonds;
using FinanceManager.Application.Services.Currencies;
using FinanceManager.Application.Services.Seeders;
using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISettingsService, SettingsService>()
                .AddScoped<PricingProvider>()
                .AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
        return services;
    }

    public static IServiceCollection AddApplicationApi(this IServiceCollection services)
    {
        services.AddScoped<ISettingsService, SettingsService>()
                .AddScoped<IMoneyFlowService, MoneyFlowService>()
                .AddScoped<IBalanceService, BalanceService>()

                .AddScoped<IAssetsServiceTyped, AssetsServiceCurrency>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceBond>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceStock>()
                .AddScoped<IAssetsService, AssetsService>()
                .AddScoped<UsersService>()

                .AddScoped<ILiabilitiesService, LiabilitiesService>()
                .AddScoped<PricingProvider>()
                .AddScoped<GuestAccountSeeder>()
                .AddScoped<ISeeder, GuestAccountSeeder>()
                .AddScoped<ISeeder, AdminAccountSeeder>()
                .AddScoped<ISeeder, BondDetailsSeeder>()
                .AddScoped<ISeeder, StockDetailsSeeder>()
                .AddScoped<ISeeder, FinancialLabelSeeder>()
                .AddScoped<IUserPlanVerifier, UserPlanVerifier>()
                .AddScoped<IAdministrationUsersService, AdministrationUsersService>()
                .AddScoped<ICurrencyExchangeService, CurrencyExchangeService>()
                .AddScoped<HttpClient>()
                .AddScoped<ICurrencyAccountImportService, CurrencyAccountImportService>()
                .AddScoped<IStockPriceProvider, StockPriceProvider>()
                .AddScoped<IBondService, BondService>()
                .AddScoped<IStockMarketService, StockMarketService>()
            ;

        return services;
    }
}