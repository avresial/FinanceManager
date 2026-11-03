using FinanceManager.Infrastructure.Features.Mcp.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using System.Security.Cryptography.X509Certificates;

namespace FinanceManager.Api.Features.Mcp.OAuth;

/// <summary>
/// MCP OAuth wiring: the short-lived authorization-session cookie, the OpenIddict authorization server
/// and validation (only when <see cref="McpOAuthOptions.Enabled"/>), and the MCP authorization policy.
/// </summary>
public static class McpOAuthServiceCollectionExtensions
{
    public static IServiceCollection AddMcpOAuth(
        this IServiceCollection services,
        AuthenticationBuilder authentication,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        authentication.AddCookie(McpOAuthAuthentication.SessionScheme, options =>
        {
            options.Cookie.Name = "fm_mcp_authorization";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.Path = "/connect";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Test")
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = configuration.GetValue(
                $"{McpOAuthOptions.SectionName}:AuthorizationSessionLifetime",
                TimeSpan.FromMinutes(10));
            options.SlidingExpiration = false;
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
                OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }
            };
        });

        var mcpOAuth = configuration.GetSection(McpOAuthOptions.SectionName).Get<McpOAuthOptions>() ?? new();
        if (mcpOAuth.Enabled)
        {
            var allowHttp = environment.IsDevelopment() || environment.IsEnvironment("Test");
            authentication
                .AddPolicyScheme(McpOAuthAuthentication.SchemeName, "MCP OAuth", options =>
                {
                    options.ForwardAuthenticate = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                    options.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme;
                    options.ForwardForbid = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                })
                .AddMcp(options => options.ResourceMetadata = new()
                {
                    Resource = mcpOAuth.Resource,
                    AuthorizationServers = { mcpOAuth.Issuer },
                    ScopesSupported = ["mcp"],
                    ResourceName = mcpOAuth.DisplayName
                });

            services.AddOpenIddict()
                .AddServer(options =>
                {
                    options.Configure(serverOptions =>
                        serverOptions.CodeChallengeMethods.Remove(OpenIddictConstants.CodeChallengeMethods.Plain));
                    options.SetIssuer(new Uri(mcpOAuth.Issuer))
                        .SetAuthorizationEndpointUris("/connect/authorize")
                        .SetTokenEndpointUris("/connect/token")
                        .AllowAuthorizationCodeFlow()
                        .AllowRefreshTokenFlow()
                        .UseReferenceAccessTokens()
                        .RegisterScopes("mcp")
                        .RegisterResources(mcpOAuth.Resource)
                        .SetAuthorizationCodeLifetime(mcpOAuth.AuthorizationCodeLifetime)
                        .SetAccessTokenLifetime(mcpOAuth.AccessTokenLifetime)
                        .SetRefreshTokenLifetime(mcpOAuth.RefreshTokenLifetime)
                        .SetRefreshTokenReuseLeeway(TimeSpan.Zero);

                    if (environment.IsEnvironment("Test"))
                    {
                        options.AddEphemeralEncryptionKey()
                            .AddEphemeralSigningKey();
                    }
                    else if (environment.IsDevelopment())
                    {
                        options.AddDevelopmentEncryptionCertificate()
                            .AddDevelopmentSigningCertificate();
                    }
                    else
                    {
                        options.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                            mcpOAuth.SigningCertificatePath!, mcpOAuth.SigningCertificatePassword));
                        options.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                            mcpOAuth.EncryptionCertificatePath!, mcpOAuth.EncryptionCertificatePassword));
                    }

                    var aspNetCore = options.UseAspNetCore().EnableAuthorizationEndpointPassthrough();
                    if (allowHttp)
                        aspNetCore.DisableTransportSecurityRequirement();
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.EnableAuthorizationEntryValidation();
                    options.EnableTokenEntryValidation();
                    options.UseAspNetCore();
                });
        }

        services.AddScoped<McpOAuthBridgeTokenValidator>();
        services.AddAuthorization(options => options.AddPolicy(McpOAuthAuthentication.PolicyName, policy =>
        {
            policy.AddAuthenticationSchemes(McpOAuthAuthentication.SchemeName);
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context =>
                context.User.HasScope("mcp") && context.User.GetAudiences().Contains(mcpOAuth.Resource, StringComparer.Ordinal));
        }));

        return services;
    }
}