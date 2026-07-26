using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Mcp.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public sealed class McpOAuthBridgeControllerTests : IDisposable
{
    private readonly FinanceManagerApiTestApp _app = new(environmentName: "Release");

    [Fact]
    public async Task Bridge_WhenMcpOAuthIsDisabled_ReturnsNotFound()
    {
        using var response = await _app.Client.PostAsync(
            "/api/Auth/oauth-bridge",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = "unused",
                ["returnUrl"] = "https://localhost/connect/authorize"
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose() => _app.Dispose();
}