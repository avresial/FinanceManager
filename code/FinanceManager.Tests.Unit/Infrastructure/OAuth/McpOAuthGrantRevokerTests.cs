using FinanceManager.Infrastructure.OAuth;
using Moq;
using OpenIddict.Abstractions;

namespace FinanceManager.Tests.Unit.Infrastructure.OAuth;

public sealed class McpOAuthGrantRevokerTests
{
    [Fact]
    public async Task RevokeClientAsync_UsesBulkOpenIddictRevocation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var application = new object();
        var applications = new Mock<IOpenIddictApplicationManager>();
        var authorizations = new Mock<IOpenIddictAuthorizationManager>();
        var tokens = new Mock<IOpenIddictTokenManager>();
        applications.Setup(manager => manager.FindByClientIdAsync("mcp-client", cancellationToken))
            .ReturnsAsync(application);
        applications.Setup(manager => manager.GetIdAsync(application, cancellationToken))
            .ReturnsAsync("application-id");
        authorizations.Setup(manager => manager.RevokeByApplicationIdAsync("application-id", cancellationToken))
            .ReturnsAsync(2);
        tokens.Setup(manager => manager.RevokeByApplicationIdAsync("application-id", cancellationToken))
            .ReturnsAsync(3);
        var revoker = new McpOAuthGrantRevoker(applications.Object, authorizations.Object, tokens.Object);

        var result = await revoker.RevokeClientAsync("mcp-client", cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.Authorizations);
        Assert.Equal(3, result.Tokens);
    }

    [Fact]
    public async Task RevokeClientAsync_ReturnsNullForUnknownClient()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var applications = new Mock<IOpenIddictApplicationManager>();
        var authorizations = new Mock<IOpenIddictAuthorizationManager>();
        var tokens = new Mock<IOpenIddictTokenManager>();
        applications.Setup(manager => manager.FindByClientIdAsync("missing", cancellationToken))
            .ReturnsAsync((object?)null);
        var revoker = new McpOAuthGrantRevoker(applications.Object, authorizations.Object, tokens.Object);

        var result = await revoker.RevokeClientAsync("missing", cancellationToken);

        Assert.Null(result);
        authorizations.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }
}