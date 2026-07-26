using FinanceManager.Api.Features.Mcp;
using FinanceManager.Infrastructure.OAuth;

namespace FinanceManager.Tests.Unit.Api.Features.Mcp;

public sealed class McpClientRevocationCommandTests
{
    [Fact]
    public async Task RunAsync_ReturnsUsageErrorForInvalidArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await McpClientRevocationCommand.RunAsync(
            ["--revoke-mcp-client"],
            _ => throw new InvalidOperationException("Revocation should not run."),
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", error.ToString());
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsNotFoundForUnknownClient()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await McpClientRevocationCommand.RunAsync(
            ["--revoke-mcp-client", "missing"],
            _ => Task.FromResult<McpOAuthRevocationResult?>(null),
            output,
            error);

        Assert.Equal(3, exitCode);
        Assert.Contains("'missing' was not found", error.ToString());
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsSuccessAndReportsRevocationCounts()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await McpClientRevocationCommand.RunAsync(
            ["--revoke-mcp-client", "mcp-client"],
            clientId =>
            {
                Assert.Equal("mcp-client", clientId);
                return Task.FromResult<McpOAuthRevocationResult?>(new(2, 3));
            },
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Revoked 2 authorization(s) and 3 token(s)", output.ToString());
        Assert.Empty(error.ToString());
    }
}