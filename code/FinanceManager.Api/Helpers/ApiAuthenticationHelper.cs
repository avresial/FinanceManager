using System.Security.Claims;

namespace FinanceManager.Api.Helpers
{
    public static class ApiAuthenticationHelper
    {
        public static int? GetUserId(ClaimsPrincipal? user)
        {
            if (user is null) return null;
            var userIdValue = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (userIdValue is null) return null;

            return int.Parse(userIdValue);

        }
    }
}
