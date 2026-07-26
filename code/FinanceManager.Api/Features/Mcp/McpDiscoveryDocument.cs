using System.Text.Json.Serialization;

namespace FinanceManager.Api.Features.Mcp;

public sealed record McpDiscoveryDocument(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("mcp_endpoint")] string McpEndpoint,
    [property: JsonPropertyName("authorization_server")] string AuthorizationServer,
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("scopes_supported")] string[] ScopesSupported);