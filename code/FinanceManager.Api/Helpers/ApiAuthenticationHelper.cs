namespace FinanceManager.Api.Helpers
{
    public static class ApiAuthenticationHelper
    {
        public static int? GetUserId(System.Security.Claims.ClaimsPrincipal? user)
        {
            if (user is null) return null;
            var userIdValue = user.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;

            if (userIdValue is null) return null;

            return int.Parse(userIdValue);

        }
    }
}
