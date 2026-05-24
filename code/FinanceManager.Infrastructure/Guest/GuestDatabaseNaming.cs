using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure.Guest;

public static class GuestDatabaseNaming
{
    public static string DatabaseNameFor(int guestUserId) => $"guest-{guestUserId}";

    public static string? TryGetGuestDatabaseName(IServiceProvider serviceProvider)
    {
        var accessor = serviceProvider.GetService<IGuestSessionAccessor>();
        var guestId = accessor?.GuestUserId;
        return guestId.HasValue ? DatabaseNameFor(guestId.Value) : null;
    }

    public static string ResolveDatabaseName(IServiceProvider serviceProvider, string defaultName)
        => TryGetGuestDatabaseName(serviceProvider) ?? defaultName;
}