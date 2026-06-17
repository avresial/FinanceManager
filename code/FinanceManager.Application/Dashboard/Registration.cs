using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Dashboard;

internal static class Registration
{
    public static IServiceCollection AddDashboardApplication(this IServiceCollection services)
    {
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        return services;
    }
}