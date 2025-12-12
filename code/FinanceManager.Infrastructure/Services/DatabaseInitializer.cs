using FinanceManager.Application.Services.Seeders;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

internal class DatabaseInitializer(IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting database initialization");

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var isRelational = dbContext.Database.IsRelational();
        if (isRelational)
        {
            try
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed. Skipping seeding.");
                return;
            }
        }
        else
        {
            logger.LogInformation("Relational database not configured. Skipping migrations.");
        }

        logger.LogInformation("Starting data seeding");
        foreach (var seeder in scope.ServiceProvider.GetServices<ISeeder>())
        {
            try
            {
                logger.LogInformation("Seeding data with {Seeder}", seeder.GetType().Name);
                await seeder.Seed(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding data with {Seeder}", seeder.GetType().Name);
            }
        }

        logger.LogInformation("Data seeding completed");
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}