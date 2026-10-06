using Xunit;

namespace FinanceManager.Tests.Integration.Shared;

// Long-lived immutable caching of fingerprinted files is only emitted for publish output, so it is not
// asserted here; these tests cover the entry points that must always be revalidated.
[Trait("Category", "Integration")]
public class StaticAssetCachingTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    [Theory]
    [InlineData("/service-worker.js")]
    [InlineData("/service-worker-assets.js")]
    [InlineData("/manifest.webmanifest")]
    [InlineData("/")]
    [InlineData("/dashboard")]
    public async Task NonFingerprintedEntryPoints_AreRevalidatedOnEveryVisit(string path)
    {
        var response = await Client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"GET {path} returned {(int)response.StatusCode}.");
        Assert.True(response.Headers.CacheControl?.NoCache, $"GET {path} should be served with Cache-Control: no-cache.");
    }
}