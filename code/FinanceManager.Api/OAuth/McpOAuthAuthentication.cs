using FinanceManager.Infrastructure.OAuth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FinanceManager.Api.OAuth;

public static class McpOAuthAuthentication
{
    public const string SchemeName = "McpOAuth";
    public const string PolicyName = "Mcp";
    public const string SessionScheme = "McpOAuthSession";
}

public sealed class McpOAuthBridgeTokenValidator(IOptionsMonitor<JwtBearerOptions> jwtOptions)
{
    public ClaimsPrincipal? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var parameters = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters.Clone();
            parameters.ClockSkew = TimeSpan.FromSeconds(30);
            return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }
}

internal static class McpOAuthReturnUrlValidator
{
    public static bool IsValid(string? returnUrl, McpOAuthOptions options)
    {
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var target) ||
            !Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer))
        {
            return false;
        }

        var authorizeEndpoint = new Uri(issuer, "connect/authorize");
        return string.IsNullOrEmpty(target.Fragment) &&
               string.Equals(target.GetLeftPart(UriPartial.Authority),
                   authorizeEndpoint.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(target.AbsolutePath, authorizeEndpoint.AbsolutePath, StringComparison.Ordinal);
    }
}