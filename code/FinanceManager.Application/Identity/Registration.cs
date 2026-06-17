using FinanceManager.Application.Identity.Lockout;
using FinanceManager.Application.Identity.PasswordReset;
using FinanceManager.Application.Identity.RefreshTokens;
using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Identity;

internal static class Registration
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<ISettingsService, SettingsService>()
                .AddScoped<IRefreshTokenService, RefreshTokenService>()
                .AddScoped<IPasswordResetService, PasswordResetService>()
                .AddScoped<IAccountLockoutService, AccountLockoutService>()
                .AddScoped<UsersService>()
                .AddScoped<IUserPlanVerifier, UserPlanVerifier>()
                .AddScoped<PricingProvider>()
                .AddScoped<GuestAccountSeeder>()
                .AddScoped<ISeeder, AdminAccountSeeder>()
                .AddScoped<ISeeder, TestUserAccountSeeder>();

        return services;
    }
}