using System.Security.Claims;

namespace FinanceManager.Api.Helpers;

public static class ApiAuthenticationHelper
{
    public static int GetUserId(ClaimsPrincipal user)
    {
        if (!user.Claims.Any(x => x.Type == ClaimTypes.NameIdentifier))
            throw new InvalidOperationException($"No {ClaimTypes.NameIdentifier} in users ClaimsPrincipal.");

        return int.Parse(user.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
    }
}