using FinanceManager.Application.Administration.Users;
using FinanceManager.Domain.Administration.Users;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Administration;

internal static class Registration
{
    public static IServiceCollection AddAdministrationApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdministrationUsersService, AdministrationUsersService>();

        return services;
    }
}