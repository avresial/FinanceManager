using FinanceManager.Api.OAuth;
using FinanceManager.Infrastructure.OAuth;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace FinanceManager.Api.Mcp;

public static class McpFeatureExtensions
{
    public static IServiceCollection AddMcpFeature(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(McpOAuthOptions.SectionName).Get<McpOAuthOptions>() ?? new();
        if (!options.Enabled)
            return services;

        services.AddMcpServer(server => server.ServerInstructions =
                "Finance Manager tools operate only on the authenticated user's financial data. " +
                "Call who_am_i to confirm the current identity before working with user-specific data.")
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithTools<IdentityTools>();
        return services;
    }

    public static IEndpointRouteBuilder MapMcpFeature(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        var options = configuration.GetSection(McpOAuthOptions.SectionName).Get<McpOAuthOptions>() ?? new();
        if (!options.Enabled)
        {
            endpoints.MapMethods("/mcp", [HttpMethods.Get, HttpMethods.Post, HttpMethods.Delete], () => Results.NotFound());
            foreach (var path in new[]
                     {
                         "/.well-known/mcp.json",
                         "/.well-known/oauth-protected-resource",
                         "/.well-known/oauth-protected-resource/mcp",
                         "/.well-known/openid-configuration",
                         "/.well-known/oauth-authorization-server"
                     })
            {
                endpoints.MapGet(path, () => Results.NotFound());
            }

            return endpoints;
        }

        endpoints.MapMcp("/mcp").RequireAuthorization(McpOAuthAuthentication.PolicyName);
        endpoints.MapGet("/.well-known/mcp.json", (IOptions<McpOAuthOptions> configuredOptions) =>
        {
            var oauth = configuredOptions.Value;
            var issuer = oauth.Issuer.TrimEnd('/');
            var clientId = oauth.Clients.FirstOrDefault()?.ClientId ?? string.Empty;
            var document = new McpDiscoveryDocument(
                Name: oauth.DisplayName,
                Version: typeof(McpFeatureExtensions).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
                Description: "Secure access to your Finance Manager data through Model Context Protocol.",
                McpEndpoint: oauth.Resource,
                AuthorizationServer: oauth.Issuer,
                AuthorizationEndpoint: $"{issuer}/connect/authorize",
                TokenEndpoint: $"{issuer}/connect/token",
                ClientId: clientId,
                ScopesSupported: ["mcp"]);
            return Results.Ok(document);
        }).AllowAnonymous();

        return endpoints;
    }
}