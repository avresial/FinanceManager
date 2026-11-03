using FinanceManager.Api.Features.Identity.Guest;
using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Api.Features.Mcp.OAuth;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace FinanceManager.Api;

/// <summary>
/// Authentication, CORS and reverse-proxy wiring for the API host. Each concern validates its own
/// configuration and fails fast outside Development when a required secret or allowlist is missing.
/// </summary>
public static class ApiSecurityExtensions
{
    private const string _developmentJwtSigningKey = "development-only-insecure-jwt-signing-key-do-not-use-in-production";

    /// <summary>
    /// Resolves and validates the JWT signing key, then wires CORS, JWT bearer authentication (including
    /// the SignalR access-token and guest-session hooks), MCP OAuth and reverse-proxy forwarded-header handling.
    /// </summary>
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var jwt = ResolveJwtSettings(configuration, environment);
        services.AddApiCors(configuration, environment);

        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options => ConfigureJwtBearer(options, jwt, environment));

        services.AddMcpOAuth(authentication, configuration, environment);
        services.AddForwardedHeaders(configuration, environment);
        return services;
    }

    private sealed record JwtSettings(string SigningKey, string Issuer, string Audience);

    private static JwtSettings ResolveJwtSettings(IConfiguration configuration, IHostEnvironment environment)
    {
        // The JWT signing key must never be committed. It is supplied via environment variable / User Secrets.
        // Fail fast outside Development so we never silently fall back to an insecure default; in Development
        // fall back to a clearly-insecure local key so the app still runs without configured secrets.
        var jwtSigningKey = configuration["JwtConfig:Key"];
        if (string.IsNullOrWhiteSpace(jwtSigningKey))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "JwtConfig:Key is not configured. Provide a signing key via the JwtConfig__Key environment variable or User Secrets.");

            jwtSigningKey = _developmentJwtSigningKey;
            configuration["JwtConfig:Key"] = jwtSigningKey;
        }
        else if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 64)
        {
            // HMAC-SHA512 (used by JwtTokenGenerator) needs a 512-bit (64-byte) key; reject anything
            // weaker so a short env var can't silently produce forgeable tokens or crash at sign time.
            throw new InvalidOperationException(
                "JwtConfig:Key must be at least 64 bytes (512 bits) for HMAC-SHA512 signing.");
        }

        var tokenValidityMins = configuration.GetValue<int>("JwtConfig:TokenValidityMins");
        if (tokenValidityMins <= 0)
            throw new InvalidOperationException(
                "JwtConfig:TokenValidityMins must be greater than 0. Set it to a positive number (e.g. 15) in appsettings or User Secrets.");

        // ValidateIssuer/ValidateAudience are enabled below, so an empty issuer/audience would reject
        // every token at request time. Fail fast at startup instead — JwtTokenGenerator mints tokens
        // with these same values, so both must be configured for auth to work at all.
        var jwtIssuer = configuration["JwtConfig:Issuer"];
        if (string.IsNullOrWhiteSpace(jwtIssuer))
            throw new InvalidOperationException(
                "JwtConfig:Issuer is not configured. Set it in appsettings or User Secrets.");

        var jwtAudience = configuration["JwtConfig:Audience"];
        if (string.IsNullOrWhiteSpace(jwtAudience))
            throw new InvalidOperationException(
                "JwtConfig:Audience is not configured. Set it in appsettings or User Secrets.");

        return new JwtSettings(jwtSigningKey, jwtIssuer, jwtAudience);
    }

    private static void AddApiCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var allowedCorsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Outside Development an empty/missing allowlist must fail fast: never silently fall back to
        // AllowAnyOrigin() in production, which would let any site call the API cross-origin.
        if (allowedCorsOrigins is not { Length: > 0 } && !environment.IsDevelopment())
            throw new InvalidOperationException(
                "Cors:AllowedOrigins is not configured. Provide explicit allowed origins via the " +
                "Cors__AllowedOrigins configuration; AllowAnyOrigin is only permitted in Development.");

        services.AddCors(options =>
        {
            options.AddPolicy("ApiCorsPolicy",
                corsPolicyBuilder =>
                {
                    if (allowedCorsOrigins is { Length: > 0 })
                    {
                        corsPolicyBuilder.WithOrigins(allowedCorsOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod();

                        return;
                    }

                    // Reachable only in Development (the guard above throws otherwise): allow any origin
                    // so local tooling and ad-hoc clients work without configuring an allowlist.
                    corsPolicyBuilder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
        });
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, JwtSettings jwt, IHostEnvironment environment)
    {
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    (path.StartsWithSegments("/hubs/currency-import")
                     || path.StartsWithSegments("/hubs/label-setter-progress")
                     || path.StartsWithSegments("/hubs/admin-logs")))
                    context.Token = accessToken;

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var principal = context.Principal;
                var isGuestClaim = principal?.FindFirst(GuestClaims.IsGuest)?.Value;
                if (!string.Equals(isGuestClaim, "true", StringComparison.OrdinalIgnoreCase))
                    return Task.CompletedTask;

                var idClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(idClaim, out var guestUserId))
                {
                    context.Fail("Guest token is missing a user id.");
                    return Task.CompletedTask;
                }

                var store = context.HttpContext.RequestServices.GetRequiredService<IGuestSessionStore>();
                if (!store.IsActive(guestUserId))
                    context.Fail("Guest session has expired.");

                return Task.CompletedTask;
            }
        };
    }

    private static void AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // The app runs behind a TLS-terminating reverse proxy (Cloudflare) in production, so the request
        // arriving at Kestrel is plain HTTP from the proxy. Honour X-Forwarded-Proto/For so Request.IsHttps,
        // Request.Scheme and the remote IP reflect the original client request — this is what makes the
        // Secure refresh-token cookie, HTTPS redirect and per-client rate limiting behave correctly.
        // Only trust headers forwarded by the declared proxy addresses/networks; trusting any source would
        // allow a client that reaches Kestrel directly to spoof scheme or IP.
        var reverseProxyIps = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        var reverseProxyNetworks = configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [];

        if (!environment.IsDevelopment() && reverseProxyIps.Length == 0 && reverseProxyNetworks.Length == 0)
            throw new InvalidOperationException(
                "ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks must be configured outside Development. " +
                "Set these to the IP addresses or CIDR ranges of your reverse proxy (e.g. Cloudflare's published ranges).");

        // Parse and validate every entry up front. A silently-dropped typo would shrink the trusted-proxy
        // set (or empty it entirely), leaving the app unable to see the real client scheme/IP — fail fast.
        var knownProxies = new List<System.Net.IPAddress>();
        foreach (var proxy in reverseProxyIps)
        {
            if (!System.Net.IPAddress.TryParse(proxy, out var ip))
                throw new InvalidOperationException(
                    $"ReverseProxy:KnownProxies contains an invalid IP address: '{proxy}'.");
            knownProxies.Add(ip);
        }

        var knownNetworks = new List<System.Net.IPNetwork>();
        foreach (var network in reverseProxyNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out var net))
                throw new InvalidOperationException(
                    $"ReverseProxy:KnownNetworks contains an invalid CIDR network: '{network}'.");
            knownNetworks.Add(net);
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var ip in knownProxies)
                options.KnownProxies.Add(ip);

            foreach (var net in knownNetworks)
                options.KnownIPNetworks.Add(net);
        });
    }
}