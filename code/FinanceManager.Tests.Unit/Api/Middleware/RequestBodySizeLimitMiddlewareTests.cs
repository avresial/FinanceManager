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
    public async Task Invoke_WhenRequestCanHaveBodyButContentLengthMissing_CallsNext()
    {
        var nextCalled = false;
        var middleware = new RequestBodySizeLimitMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<RequestBodySizeLimitMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = RequestBodySizeLimits.CurrencyImportEntriesPath;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature(canHaveBody: true));

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private sealed class TestRequestBodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }
}