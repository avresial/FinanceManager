using FinanceManager.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

internal sealed class AdminAccountSeederBackgroundService(IServiceProvider serviceProvider, ILogger<AdminAccountSeederBackgroundService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var seeder = scope.ServiceProvider.GetService<AdminAccountSeeder>();
            if (seeder is null)
            {
                logger.LogWarning("AdminAccountSeeder not registered in DI. Skipping admin seeding.");
                return;
            }

            await seeder.Seed();
            logger.LogInformation("Admin account seeding finished.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while running AdminAccountSeederBackgroundService");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
