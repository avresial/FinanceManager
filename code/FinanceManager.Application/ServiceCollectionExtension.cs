using FinanceManager.Application.Administration;
using FinanceManager.Application.Dashboard;
using FinanceManager.Application.FinancialAccounts;
using FinanceManager.Application.Identity;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Application.Insights;
using FinanceManager.Application.Labels;
using FinanceManager.Application.MoneyFlow;
using FinanceManager.Application.Shared.Maintenance;
using FinanceManager.Application.Shared.Time;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>()
                .AddScoped<ISettingsService, SettingsService>()
                .AddScoped<PricingProvider>()
                .AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
        return services;
    }

    public static IServiceCollection AddApplicationApi(this IServiceCollection services)
    {
        // Identity must come before FinancialAccounts and Labels: seeders run in
        // ISeeder registration order (admin/test users, then financial-account
        // detail seeders, then labels).
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IMaintenanceKeyService, MaintenanceKeyService>();

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