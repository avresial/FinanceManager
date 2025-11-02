using FinanceManager.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

internal sealed class GuestAccountSeederBackgroundService(IServiceProvider serviceProvider, ILogger<GuestAccountSeederBackgroundService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var guestAccountSeeder = scope.ServiceProvider.GetService<GuestAccountSeeder>();
            if (guestAccountSeeder is null)
            {
                logger.LogWarning("GuestAccountSeeder not registered in DI. Skipping guest seeding.");
                return;
            }

            await guestAccountSeeder.SeedNewData(DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow);
            logger.LogInformation("Guest account seeding finished.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while running GuestAccountSeederBackgroundService");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}