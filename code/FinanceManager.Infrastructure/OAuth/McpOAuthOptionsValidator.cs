using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.OAuth;

public sealed class McpOAuthOptionsValidator(IHostEnvironment environment) : IValidateOptions<McpOAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, McpOAuthOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();
        var allowLoopbackHttp = environment.IsDevelopment() || environment.IsEnvironment("Test");

        ValidateUri(options.Issuer, "McpOAuth:Issuer", allowLoopbackHttp, failures);
        ValidateUri(options.Resource, "McpOAuth:Resource", allowLoopbackHttp, failures);
        ValidateUri(options.LoginUrl, "McpOAuth:LoginUrl", allowLoopbackHttp, failures);

        if (string.IsNullOrWhiteSpace(options.DisplayName))
            failures.Add("McpOAuth:DisplayName must be configured.");
        var clients = options.Clients ?? [];
        if (clients.Length == 0)
            failures.Add("McpOAuth:Clients must contain at least one client.");
        if (clients.Select(client => client.ClientId).Distinct(StringComparer.Ordinal).Count() != clients.Length)
            failures.Add("McpOAuth client IDs must be unique.");

        foreach (var client in clients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
                failures.Add("Every MCP OAuth client must have a client ID.");
            if (string.IsNullOrWhiteSpace(client.DisplayName))
                failures.Add($"MCP OAuth client '{client.ClientId}' must have a display name.");
            var redirectUris = client.RedirectUris ?? [];
            if (redirectUris.Length == 0)
                failures.Add($"MCP OAuth client '{client.ClientId}' must have at least one redirect URI.");
            foreach (var redirectUri in redirectUris)
                ValidateUri(redirectUri, $"MCP OAuth client '{client.ClientId}' redirect URI", allowLoopbackHttp, failures);
        }

        if (!allowLoopbackHttp)
        {
            if (string.IsNullOrWhiteSpace(options.SigningCertificatePath))
                failures.Add("McpOAuth:SigningCertificatePath must be configured outside Development/Test.");
            if (string.IsNullOrWhiteSpace(options.EncryptionCertificatePath))
                failures.Add("McpOAuth:EncryptionCertificatePath must be configured outside Development/Test.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateUri(string value, string setting, bool allowLoopbackHttp, List<string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             !(allowLoopbackHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
        {
            failures.Add($"{setting} must use HTTPS, or loopback HTTP in Development/Test.");
        }
    }
}