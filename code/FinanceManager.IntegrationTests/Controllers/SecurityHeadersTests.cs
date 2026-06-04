using System.Net.Http.Headers;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class SecurityHeadersTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    [Fact]
    public async Task Response_Includes_StandardSecurityHeaders()
    {
        var response = await Client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);

        Assert.Equal("nosniff", Assert.Single(GetHeader(response, "X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(GetHeader(response, "X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(GetHeader(response, "Referrer-Policy")));

        var csp = Assert.Single(GetHeader(response, "Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("'wasm-unsafe-eval'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    private static IEnumerable<string> GetHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var responseValues))
            return responseValues;

        if (response.Content.Headers.TryGetValues(name, out var contentValues))
            return contentValues;

        Assert.Fail($"Expected response to contain header '{name}'.");
        return [];
    }
}