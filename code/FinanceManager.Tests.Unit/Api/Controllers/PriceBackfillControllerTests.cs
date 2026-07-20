using FinanceManager.Api.Controllers;
using FinanceManager.Api.Services;
using FinanceManager.Application.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Unit.Api.Controllers;

[Trait("Category", "Unit")]
public class PriceBackfillControllerTests
{
    private const string _configuredKey = "test-maintenance-key";

    private readonly Mock<IPriceBackfillJobChannel> _channel = new();

    private PriceBackfillController CreateController(string? configuredKey, string? providedKey)
    {
        var httpContext = new DefaultHttpContext();
        if (providedKey is not null)
            httpContext.Request.Headers[PriceBackfillController.ApiKeyHeader] = providedKey;

        return new PriceBackfillController(
            Options.Create(new MaintenanceOptions { ApiKey = configuredKey ?? string.Empty }),
            _channel.Object,
            NullLogger<PriceBackfillController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Trigger_WithoutConfiguredKey_ReturnsNotFound(string? configuredKey)
    {
        var result = CreateController(configuredKey, _configuredKey).Trigger();

        Assert.IsType<NotFoundResult>(result);
        _channel.Verify(x => x.TryQueueJob(It.IsAny<PriceBackfillJobRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    [InlineData("")]
    public void Trigger_WithMissingOrWrongKey_ReturnsUnauthorized(string? providedKey)
    {
        var result = CreateController(_configuredKey, providedKey).Trigger();

        Assert.IsType<UnauthorizedResult>(result);
        _channel.Verify(x => x.TryQueueJob(It.IsAny<PriceBackfillJobRequest>()), Times.Never);
    }

    [Fact]
    public void Trigger_WithValidKey_QueuesPastWeekAndReturnsAccepted()
    {
        PriceBackfillJobRequest? queued = null;
        _channel
            .Setup(x => x.TryQueueJob(It.IsAny<PriceBackfillJobRequest>()))
            .Callback((PriceBackfillJobRequest request) => queued = request)
            .Returns(true);

        var result = CreateController(_configuredKey, _configuredKey).Trigger();

        Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(queued);
        Assert.Equal(DateTime.UtcNow.Date, queued.End);
        Assert.Equal(queued.End.AddDays(-7), queued.Start);
    }

    [Fact]
    public void Trigger_WhenRunAlreadyQueued_StillReturnsAccepted()
    {
        _channel.Setup(x => x.TryQueueJob(It.IsAny<PriceBackfillJobRequest>())).Returns(false);

        var result = CreateController(_configuredKey, _configuredKey).Trigger();

        Assert.IsType<AcceptedResult>(result);
    }
}