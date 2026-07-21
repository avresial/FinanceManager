using OpenIddict.Abstractions;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace FinanceManager.Infrastructure.OAuth;

internal sealed class McpOAuthConfigurationReconciler(
    IOpenIddictApplicationManager applications,
    IOpenIddictScopeManager scopes)
{
    private const string _managedProperty = "finance_manager_mcp_managed";

    public async Task ReconcileAsync(McpOAuthOptions options, CancellationToken cancellationToken)
    {
        var configuredClientIds = options.Clients.Select(client => client.ClientId).ToHashSet(StringComparer.Ordinal);
        foreach (var configuredClient in options.Clients)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = configuredClient.ClientId,
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Implicit,
                DisplayName = configuredClient.DisplayName
            };
            descriptor.Properties[_managedProperty] = JsonSerializer.SerializeToElement(true);
            foreach (var redirectUri in configuredClient.RedirectUris)
                descriptor.RedirectUris.Add(new Uri(redirectUri));
            descriptor.Permissions.UnionWith([
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + "mcp",
                Permissions.Prefixes.Resource + options.Resource
            ]);
            if (configuredClient.RequirePkce)
                descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

            var application = await applications.FindByClientIdAsync(configuredClient.ClientId, cancellationToken);
            if (application is null)
                await applications.CreateAsync(descriptor, cancellationToken);
            else
                await applications.UpdateAsync(application, descriptor, cancellationToken);
        }

        List<object> staleApplications = [];
        await foreach (var application in applications.ListAsync(null, null, cancellationToken))
        {
            var properties = await applications.GetPropertiesAsync(application, cancellationToken);
            var clientId = await applications.GetClientIdAsync(application, cancellationToken);
            if (properties.TryGetValue(_managedProperty, out var managed) && managed.ValueKind == JsonValueKind.True &&
                clientId is not null && !configuredClientIds.Contains(clientId))
            {
                staleApplications.Add(application);
            }
        }
        foreach (var application in staleApplications)
            await applications.DeleteAsync(application, cancellationToken);

        var scopeDescriptor = new OpenIddictScopeDescriptor
        {
            Name = "mcp",
            DisplayName = options.DisplayName
        };
        scopeDescriptor.Resources.Add(options.Resource);

        var existingScope = await scopes.FindByNameAsync("mcp", cancellationToken);
        if (existingScope is null)
            await scopes.CreateAsync(scopeDescriptor, cancellationToken);
        else
            await scopes.UpdateAsync(existingScope, scopeDescriptor, cancellationToken);
    }
}
