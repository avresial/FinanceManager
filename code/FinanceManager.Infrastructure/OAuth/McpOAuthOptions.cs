namespace FinanceManager.Infrastructure.OAuth;

public sealed class McpOAuthOptions
{
    public const string SectionName = "McpOAuth";

    public bool Enabled { get; init; }
    public string Issuer { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string LoginUrl { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "Finance Manager MCP";
    public string? SigningCertificatePath { get; init; }
    public string? SigningCertificatePassword { get; init; }
    public string? EncryptionCertificatePath { get; init; }
    public string? EncryptionCertificatePassword { get; init; }
    public McpOAuthClientOptions[] Clients { get; init; } = [];
}

public sealed class McpOAuthClientOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool RequirePkce { get; init; } = true;
    public string[] RedirectUris { get; init; } = [];
}