using FinanceManager.Application.Administration;
using FinanceManager.Application.FinancialAccounts;
using FinanceManager.Application.Identity;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Application.Insights;
using FinanceManager.Application.Labels;
using FinanceManager.Application.MoneyFlow;
using FinanceManager.Application.Services;
using FinanceManager.Application.Services.Ai;
using FinanceManager.Application.Services.Seeders;
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
        // Seeders run in ISeeder registration order: identity's admin/test users
        // first, then the financial-account detail seeders, then labels.
        services.AddIdentityApplication();
        services.AddFinancialAccountsApplication();
        services.AddLabelsApplication();
        services.AddInsightsApplication();
        services.AddMoneyFlowApplication();

        services.AddAdministrationApplication();

        return services;
    }
}