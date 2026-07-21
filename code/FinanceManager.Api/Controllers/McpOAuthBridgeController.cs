using FinanceManager.Api.OAuth;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Infrastructure.OAuth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FinanceManager.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/Auth/oauth-bridge")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public sealed class McpOAuthBridgeController(
    IOptions<McpOAuthOptions> options,
    McpOAuthBridgeTokenValidator tokenValidator,
    IUserRepository users,
    IAntiforgery antiforgery,
    ILogger<McpOAuthBridgeController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Bridge([FromForm] McpOAuthBridgeRequest request)
    {
        if (!options.Value.Enabled)
            return NotFound();
        if (!await ValidateAntiforgeryToken())
            return Forbid();
        if (!McpOAuthReturnUrlValidator.IsValid(request.ReturnUrl, options.Value))
            return BadRequest(new { message = "The return URL is invalid." });

        var principal = tokenValidator.Validate(request.Token);
        if (principal is null)
            return Unauthorized();
        if (string.Equals(principal.FindFirstValue(GuestClaims.IsGuest), "true", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            await users.GetUser(userId) is null)
        {
            return Unauthorized();
        }

        var sessionIdentity = new ClaimsIdentity(McpOAuthAuthentication.SessionScheme);
        sessionIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        await HttpContext.SignInAsync(
            McpOAuthAuthentication.SessionScheme,
            new ClaimsPrincipal(sessionIdentity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(options.Value.AuthorizationSessionLifetime)
            });

        return Redirect(request.ReturnUrl!);
    }

    private async Task<bool> ValidateAntiforgeryToken()
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning(ex, "Rejected MCP OAuth bridge request with an invalid antiforgery token.");
            return false;
        }
    }
}

public sealed record McpOAuthBridgeRequest(string? Token, string? ReturnUrl);