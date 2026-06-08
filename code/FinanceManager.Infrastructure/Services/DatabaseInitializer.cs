using FinanceManager.Application.Services.Seeders;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

internal class DatabaseInitializer(
    IServiceProvider serviceProvider,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting database initialization");

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (dbContext.Database.IsRelational())
        {
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (environment.IsDevelopment())
            {
                // Keep local development fast by auto-applying schema changes on startup.
                // Deployed environments must go through the explicit CI migration step instead.
                if (pendingMigrations.Any())
                {
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("Database migrations applied");
                }

                logger.LogInformation("Development environment detected. Startup migrations remain enabled only for local development.");
            }
            else
            {
                var pendingMigrationList = pendingMigrations.ToArray();
                if (pendingMigrationList.Length > 0)
                    throw new InvalidOperationException(
                        $"Pending database migrations detected ({string.Join(", ", pendingMigrationList)}). " +
                        "Apply them through the CI deployment migration step before starting the application.");
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