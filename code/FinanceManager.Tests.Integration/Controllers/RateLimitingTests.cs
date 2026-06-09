using FinanceManager.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

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
}