using FinanceManager.Application.FinancialAccounts;
using FinanceManager.Application.MoneyFlow;
using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Application.Services.Ai;
using FinanceManager.Application.Services.FinancialInsights;
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
        services.AddScoped<ISettingsService, SettingsService>()
                .AddScoped<IRefreshTokenService, RefreshTokenService>()
                .AddScoped<IPasswordResetService, PasswordResetService>()
                .AddScoped<IAccountLockoutService, AccountLockoutService>()
                .AddScoped<UsersService>()
                .AddScoped<PricingProvider>()
                .AddScoped<GuestAccountSeeder>()
                .AddScoped<FinancialInsightsSeeder>()
                .AddScoped<FinancialLabelSeeder>()
                .AddScoped<ISeeder, AdminAccountSeeder>()
                .AddScoped<ISeeder, TestUserAccountSeeder>();

        // Seeders run in ISeeder registration order: admin/test users first (above),
        // then the financial-account detail seeders, then labels (below).
        services.AddFinancialAccountsApplication();
        services.AddMoneyFlowApplication();

        services.AddScoped<ISeeder, FinancialLabelSeeder>(sp => sp.GetRequiredService<FinancialLabelSeeder>())
                // .AddScoped<ISeeder, FinancialInsightsSeeder>()
                .AddScoped<IUserPlanVerifier, UserPlanVerifier>()
                .AddScoped<IAdministrationUsersService, AdministrationUsersService>()

                .AddScoped<IFinancialInsightsAiGenerator, FinancialInsightsAiGenerator>()
                .AddScoped<ILabelSetterAiService, LabelSetterAiService>()
                .AddScoped<ILabelSuggestionAiService, LabelSuggestionAiService>()
                .AddScoped<IRecurringTransactionDetectorService, RecurringTransactionDetectorService>()
                .AddScoped<IDiversificationService, DiversificationService>();

        return services;
    }
}