using FinanceManager.Application.Administration.Users;
using FinanceManager.Domain.Administration.Users;
using FinanceManager.Domain.Services;
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