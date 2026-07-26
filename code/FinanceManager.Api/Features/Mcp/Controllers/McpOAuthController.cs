using FinanceManager.Api.Features.Mcp.OAuth;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Infrastructure.OAuth;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace FinanceManager.Api.Features.Mcp.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public sealed class McpOAuthController(
    IOptions<McpOAuthOptions> options,
    IUserRepository users) : ControllerBase
{
    [AcceptVerbs("GET", "POST")]
    [Route("connect/authorize")]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return NotFound();

        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth authorization request is unavailable.");
        var resources = request.GetResources().ToArray();
        if (resources.Length != 1 ||
            !string.Equals(resources[0], options.Value.Resource, StringComparison.Ordinal))
        {
            return OAuthError(Errors.InvalidTarget, "The requested MCP resource is invalid.");
        }

        var scopes = request.GetScopes().ToArray();
        if (!scopes.Contains("mcp", StringComparer.Ordinal) ||
            scopes.Any(scope => scope is not "mcp" and not Scopes.OfflineAccess))
        {
            return OAuthError(Errors.InvalidScope, "The MCP scope is required and no other scopes are supported.");
        }

        var session = await HttpContext.AuthenticateAsync(McpOAuthAuthentication.SessionScheme);
        var subject = session.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!session.Succeeded || !int.TryParse(subject, out var userId))
            return RedirectToLogin();

        var user = await users.GetUser(userId);
        if (user is null)
        {
            await HttpContext.SignOutAsync(McpOAuthAuthentication.SessionScheme);
            return RedirectToLogin();
        }

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);
        identity.AddClaim(Claims.Subject, user.UserId.ToString());
        identity.AddClaim(Claims.Name, user.Login);
        foreach (var role in user.UserRole.GetAssignedRoles())
            identity.AddClaim(Claims.Role, role);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(resources[0]);
        principal.SetDestinations(static _ => [Destinations.AccessToken]);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private IActionResult RedirectToLogin()
    {
        var returnUrl = Request.GetEncodedUrl();
        if (!McpOAuthReturnUrlValidator.IsValid(returnUrl, options.Value))
            return OAuthError(Errors.InvalidRequest, "The authorization request origin is invalid.");

        return Redirect(QueryHelpers.AddQueryString(options.Value.LoginUrl, "returnUrl", returnUrl));
    }

    private IActionResult OAuthError(string error, string description) => Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        }),
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}