using OpenIddict.Abstractions;
using System.Security.Claims;

namespace FinanceManager.Api.Features.Mcp;

public sealed class McpUserContext(IHttpContextAccessor httpContextAccessor)
{
    public ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("The authenticated MCP request is unavailable.");

    public int GetUserId()
    {
        var subject = Principal.FindFirstValue(OpenIddictConstants.Claims.Subject)
            ?? Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(subject, out var userId)
            ? userId
            : throw new InvalidOperationException("The authenticated MCP identity has no valid user id.");
    }
}