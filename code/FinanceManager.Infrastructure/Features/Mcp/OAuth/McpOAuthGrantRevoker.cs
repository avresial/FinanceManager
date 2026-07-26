using OpenIddict.Abstractions;

namespace FinanceManager.Infrastructure.Features.Mcp.OAuth;

public sealed class McpOAuthGrantRevoker(
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens)
{
    public async Task<McpOAuthRevocationResult?> RevokeClientAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        var application = await applications.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null) return null;
        var applicationId = await applications.GetIdAsync(application, cancellationToken)
            ?? throw new InvalidOperationException($"OpenIddict application '{clientId}' has no id.");

        var revokedAuthorizations = await authorizations.RevokeByApplicationIdAsync(applicationId, cancellationToken);
        var revokedTokens = await tokens.RevokeByApplicationIdAsync(applicationId, cancellationToken);

        return new McpOAuthRevocationResult(revokedAuthorizations, revokedTokens);
    }
}

public sealed record McpOAuthRevocationResult(long Authorizations, long Tokens);