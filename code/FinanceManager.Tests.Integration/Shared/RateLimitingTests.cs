using FinanceManager.Application.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Shared;

[Collection("api")]
[Trait("Category", "Integration")]
public class RateLimitingTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _authPermitLimit = 3;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // The shared test app disables rate limiting; re-enable it here with a tiny auth budget so a
        // handful of requests deterministically trips the 429 path without waiting on a 60s window.
        services.PostConfigure<RateLimitingOptions>(options =>
        {
            options.Enabled = true;
            options.Auth = new RateLimitPolicyOptions { PermitLimit = _authPermitLimit, WindowSeconds = 60 };
        });
    }

    [Fact]
    public async Task AuthEndpoint_ReturnsTooManyRequestsWithRetryAfter_OnceLimitExceeded()
    {
        // refresh is anonymous and dependency-free (no CSRF token => 403), so it exercises the auth policy
        // with no setup. Sending one more request than the permit limit must trip the limiter.
        HttpResponseMessage? limited = null;
        for (var attempt = 0; attempt < _authPermitLimit + 1; attempt++)
        {
            var response = await Client.PostAsync("api/Auth/refresh", content: null, TestContext.Current.CancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
                break;
            }

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.NotNull(limited);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task CsrfTokenEndpoint_IsNotBoundByTheAuthBudget()
    {
        // csrf-token is an idempotent read the client fetches (and caches) before it can refresh/logout, so it is
        // deliberately kept off the strict auth policy. Sending comfortably more requests than the auth permit
        // limit must never trip the 429 path (only the far larger global limiter applies here).
        for (var attempt = 0; attempt < _authPermitLimit * 2; attempt++)
        {
            var response = await Client.GetAsync("api/Auth/csrf-token", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Fact]
    public async Task LogoutEndpoint_ReturnsTooManyRequests_OnceLimitExceeded()
    {
        // logout carries the strict auth policy per-action (the class-level attribute was removed), so it must still
        // trip the 429 path once the budget is spent. Like refresh it 403s without a CSRF token, so no setup is
        // needed. The loop breaks on the first 429, which keeps the test robust to the auth partition already being
        // (partly) spent by the sibling refresh test that shares this class's rate limiter within the same window.
        HttpResponseMessage? limited = null;
        for (var attempt = 0; attempt < _authPermitLimit + 1; attempt++)
        {
            var response = await Client.PostAsync("api/Auth/logout", content: null, TestContext.Current.CancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
                break;
            }

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.NotNull(limited);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }
}