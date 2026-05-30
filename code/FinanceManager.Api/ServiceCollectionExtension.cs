using FinanceManager.Api.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinanceManager.Api;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        // Readiness probe dependency: surface database connectivity on the "ready" tag so /health reflects it.
        services.AddHealthChecks()
            .AddTypeActivatedCheck<DatabaseHealthCheck>("database", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

        return services;
    }
}