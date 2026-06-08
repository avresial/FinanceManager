using FinanceManager.Api;
using FinanceManager.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Unit.Api.Middleware;

[Trait("Category", "Unit")]
public class RequestBodySizeLimitMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenRequestCannotHaveBody_CallsNext()
    {
        var nextCalled = false;
        var middleware = new RequestBodySizeLimitMiddleware(
            context =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<RequestBodySizeLimitMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature(canHaveBody: false));

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_WhenRequestCanHaveBodyButContentLengthMissing_ReturnsLengthRequired()
    {
        var middleware = new RequestBodySizeLimitMiddleware(
            _ => Task.CompletedTask,
            NullLogger<RequestBodySizeLimitMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = RequestBodySizeLimits.StockImportPath;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature(canHaveBody: true));

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status411LengthRequired, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
    }

    private sealed class TestRequestBodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }
}