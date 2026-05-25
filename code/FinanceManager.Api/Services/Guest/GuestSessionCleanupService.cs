using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Guest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceManager.Api.Services.Guest;

internal sealed class GuestSessionCleanupService(
    IGuestSessionStore sessionStore,
    InMemoryDatabaseRoot inMemoryDatabaseRoot,
    ILogger<GuestSessionCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan _sessionTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan _sweepInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_sweepInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ClearExpiredSessions();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Guest session cleanup sweep failed.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void ClearExpiredSessions()
    {
        var expired = sessionStore.GetExpired(_sessionTtl);
        if (expired.Count == 0) return;

        foreach (var guestUserId in expired)
        {
            try
            {
                DropGuestDatabase(guestUserId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to drop in-memory database for guest {GuestUserId}", guestUserId);
            }
            finally
            {
                sessionStore.Remove(guestUserId);
            }
        }

        logger.LogInformation("Cleared {Count} expired guest session(s).", expired.Count);
    }

    // The shared InMemoryDatabaseRoot is required: by default EF Core's InMemory provider keeps a separate store
    // per internal service provider, so a standalone DbContext built here would otherwise target a different
    // store than the DI-resolved contexts and silently fail to clear anything.
    private void DropGuestDatabase(int guestUserId)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(GuestDatabaseNaming.DatabaseNameFor(guestUserId), inMemoryDatabaseRoot);

        using var context = new AppDbContext(optionsBuilder.Options);
        context.Database.EnsureDeleted();
    }
}