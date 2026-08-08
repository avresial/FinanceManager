using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Infrastructure.Features.Mcp.OAuth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Persistence;

internal class DatabaseInitializer(
    IServiceProvider serviceProvider,
    IHostEnvironment environment,
    IHostApplicationLifetime applicationLifetime,
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

        var oauthOptions = scope.ServiceProvider.GetRequiredService<IOptions<McpOAuthOptions>>().Value;
        if (oauthOptions.Enabled)
        {
            var oauthReconciler = scope.ServiceProvider.GetRequiredService<McpOAuthConfigurationReconciler>();
            await oauthReconciler.ReconcileAsync(oauthOptions, cancellationToken);
        }

        _ = Task.Run(() => SeedData(applicationLifetime.ApplicationStopping), CancellationToken.None);
        logger.LogInformation("Data seeding scheduled in the background");
    }

    private async Task SeedData(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting data seeding");

        using var scope = serviceProvider.CreateScope();
        foreach (var seeder in scope.ServiceProvider.GetServices<ISeeder>())
        {
            try
            {
                logger.LogInformation("Seeding data with {Seeder}", seeder.GetType().Name);
                await seeder.Seed(cancellationToken);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Data seeding cancelled during application shutdown while running {Seeder}.", seeder.GetType().Name);
                return;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Data seeding cancelled or timed out while running {Seeder}.", seeder.GetType().Name);
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