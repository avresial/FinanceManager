using FinanceManager.Application.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace FinanceManager.Api;

/// <summary>
/// Wires up ASP.NET Core rate limiting. A lenient global limiter guards every request; named policies
/// (<see cref="AuthPolicy"/>, <see cref="ExternalProviderPolicy"/>) are attached to the controllers/actions
/// that are most attractive to abuse. Limits are read from <see cref="RateLimitingOptions"/> at request time
/// so they can be tuned via configuration (and disabled in tests) without re-registering the limiter.
/// </summary>
public static class RateLimitingServiceCollectionExtension
{
    /// <summary>Policy guarding authentication endpoints against brute-force / credential stuffing.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Policy guarding endpoints that fan out to paid/quota'd external providers.</summary>
    public const string ExternalProviderPolicy = "external-provider";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // The global limiter applies to every request in addition to any endpoint-specific policy, so auth and
            // external-provider endpoints are bounded by whichever of (global, their policy) is hit first.
            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                CreatePartition(httpContext, "global", o => o.Global));

            limiterOptions.AddPolicy(AuthPolicy, httpContext => CreatePartition(httpContext, "auth", o => o.Auth));
            limiterOptions.AddPolicy(ExternalProviderPolicy, httpContext => CreatePartition(httpContext, "ext", o => o.ExternalProvider));

            limiterOptions.OnRejected = async (context, token) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                // Always surface Retry-After. The fixed-window limiter reports the time until the window resets;
                // fall back to the configured global window if the metadata is unavailable.
                int retryAfterSeconds;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                else
                    retryAfterSeconds = context.HttpContext.RequestServices
                        .GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue.Global.WindowSeconds;

                response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                await response.WriteAsync("Too many requests. Please retry later.", token);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(
        HttpContext httpContext, string scope, Func<RateLimitingOptions, RateLimitPolicyOptions> selectPolicy)
    {
        var options = httpContext.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue;
        if (!options.Enabled)
            return RateLimitPartition.GetNoLimiter("disabled");

        var policy = selectPolicy(options);
        return RateLimitPartition.GetFixedWindowLimiter($"{scope}:{GetPartitionKey(httpContext)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = policy.PermitLimit,
            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
            QueueLimit = policy.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    private static string GetPartitionKey(HttpContext httpContext)
    {
        // Partition by authenticated user when present so callers sharing a NAT'd IP don't exhaust each other's
        // budget; fall back to the remote IP for anonymous traffic (login, refresh, public stock reads).
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrEmpty(ip) ? "anonymous" : $"ip:{ip}";
    }
}