using FinanceManager.Application.Administration;
using FinanceManager.Application.Dashboard;
using FinanceManager.Application.FinancialAccounts;
using FinanceManager.Application.Identity;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Application.Insights;
using FinanceManager.Application.Labels;
using FinanceManager.Application.MoneyFlow;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
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
        // Identity must come before FinancialAccounts and Labels: seeders run in
        // ISeeder registration order (admin/test users, then financial-account
        // detail seeders, then labels).
        services
            .AddIdentityApplication()
            .AddFinancialAccountsApplication()
            .AddLabelsApplication()
            .AddInsightsApplication()
            .AddMoneyFlowApplication()
            .AddDashboardApplication()
            .AddAdministrationApplication();

        return services;
    }
}