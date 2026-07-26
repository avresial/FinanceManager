using System.Security.Claims;

namespace FinanceManager.Api.Shared.Helpers;

public static class ApiAuthenticationHelper
{
    public static int GetUserId(ClaimsPrincipal user)
    {
        if (!user.Claims.Any(x => x.Type == ClaimTypes.NameIdentifier))
            throw new InvalidOperationException($"No {ClaimTypes.NameIdentifier} in users ClaimsPrincipal.");

        return int.Parse(user.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
    }

    public static bool IsAuthenticatedUser(ClaimsPrincipal user, int userId) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var authenticatedUserId) &&
        authenticatedUserId == userId;

    public static bool IsAccountOwner(ClaimsPrincipal user, int accountUserId) =>
        IsAuthenticatedUser(user, accountUserId);

    public static bool IsAdmin(ClaimsPrincipal user) =>
        user.IsInRole("Admin");

    public static bool IsAdminOrAuthenticatedUser(ClaimsPrincipal user, int userId) =>
        IsAdmin(user) || IsAuthenticatedUser(user, userId);
}