using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Guest;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Services.Guest;

internal sealed class GuestSessionCleanupService(
    IGuestSessionStore sessionStore,
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

    private static void DropGuestDatabase(int guestUserId)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(GuestDatabaseNaming.DatabaseNameFor(guestUserId));

        using var context = new AppDbContext(optionsBuilder.Options);
        context.Database.EnsureDeleted();
    }
}
