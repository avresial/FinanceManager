using FinanceManager.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.UnitTests.Api.Middleware;

[Trait("Category", "Unit")]
public class GlobalExceptionHandlerTests
{
    private static (GlobalExceptionHandler Handler, List<ProblemDetailsContext> Written) CreateHandler(string environmentName)
    {
        var written = new List<ProblemDetailsContext>();

        var problemDetailsService = new Mock<IProblemDetailsService>();
        problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(written.Add)
            .Returns(ValueTask.FromResult(true));

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        var handler = new GlobalExceptionHandler(
            problemDetailsService.Object,
            environment.Object,
            NullLogger<GlobalExceptionHandler>.Instance);

        return (handler, written);
    }

    [Fact]
    public async Task TryHandleAsync_WritesProblemDetailsWith500AndReturnsTrue()
    {
        var (handler, written) = CreateHandler(Environments.Production);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var problem = Assert.Single(written).ProblemDetails;
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
    }

    [Fact]
    public async Task TryHandleAsync_InProduction_DoesNotLeakStackTrace()
    {
        var (handler, written) = CreateHandler(Environments.Production);

        await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("boom"), CancellationToken.None);

        var problem = Assert.Single(written).ProblemDetails;
        Assert.DoesNotContain("InvalidOperationException", problem.Detail);
        Assert.DoesNotContain("boom", problem.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_InDevelopment_IncludesExceptionDetail()
    {
        var (handler, written) = CreateHandler(Environments.Development);

        await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("boom"), CancellationToken.None);

        var problem = Assert.Single(written).ProblemDetails;
        Assert.Contains("boom", problem.Detail);
    }
}