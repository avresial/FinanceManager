using ModelContextProtocol.Server;
using OpenIddict.Abstractions;
using System.ComponentModel;
using System.Security.Claims;

namespace FinanceManager.Api.Mcp;

[McpServerToolType]
public sealed class IdentityTools(IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(Name = "who_am_i")]
    [Description("Return the authenticated Finance Manager user's id, login, and roles.")]
    public McpIdentity WhoAmI()
    {
        var user = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("The authenticated MCP request is unavailable.");
        var subject = user.FindFirstValue(OpenIddictConstants.Claims.Subject)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The authenticated MCP identity has no subject.");
        var login = user.FindFirstValue(OpenIddictConstants.Claims.Name)
            ?? user.Identity?.Name
            ?? string.Empty;
        var roles = user.FindAll(OpenIddictConstants.Claims.Role)
            .Concat(user.FindAll(ClaimTypes.Role))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new McpIdentity(subject, login, roles);
    }
}

public sealed record McpIdentity(
    [property: Description("Stable Finance Manager user id.")] string UserId,
    [property: Description("Finance Manager login name.")] string Login,
    [property: Description("Roles granted to the user.")] string[] Roles);