using FinanceManager.Api.Features.Maintenance.Controllers;
using FinanceManager.Api.Features.Maintenance.Services;
using FinanceManager.Application.Shared.Maintenance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Api.Features.Maintenance.Controllers;

[Trait("Category", "Unit")]
public class PriceBackfillControllerTests
{
    private const string _validKey = "fmk_test-maintenance-key";

    private readonly Mock<IMaintenanceKeyService> _keyService = new();
    private readonly Mock<IPriceBackfillJobChannel> _channel = new();

    private PriceBackfillController CreateController(string? providedKey)
    {
        var httpContext = new DefaultHttpContext();
        if (providedKey is not null)
            httpContext.Request.Headers[PriceBackfillController.ApiKeyHeader] = providedKey;

        return new PriceBackfillController(
            _keyService.Object,
            _channel.Object,
            NullLogger<PriceBackfillController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private void SetupConfiguredKey()
    {
        _keyService.Setup(x => x.IsConfiguredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _keyService
            .Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => key == _validKey);
    }

    [Fact]
    public async Task Trigger_WithoutAnyConfiguredKey_ReturnsNotFound()
    {
        _keyService.Setup(x => x.IsConfiguredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController(_validKey).Trigger(TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        _channel.Verify(x => x.TryQueueJob(It.IsAny<PriceBackfillJobRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    [InlineData("")]
    public async Task Trigger_WithMissingOrWrongKey_ReturnsUnauthorized(string? providedKey)
    {
        SetupConfiguredKey();

        var result = await CreateController(providedKey).Trigger(TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result);
        _channel.Verify(x => x.TryQueueJob(It.IsAny<PriceBackfillJobRequest>()), Times.Never);
    }

    [Fact]
    public async Task Trigger_WithValidKey_QueuesPastWeekAndReturnsAccepted()
    {
        SetupConfiguredKey();
        PriceBackfillJobRequest? queued = null;
        _channel
            .Setup(x => x.TryQueueJob(It.IsAny<PriceBackfillJobRequest>()))
            .Callback((PriceBackfillJobRequest request) => queued = request)
            .Returns(true);

        var result = await CreateController(_validKey).Trigger(TestContext.Current.CancellationToken);

        Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(queued);
        Assert.Equal(DateTime.UtcNow.Date, queued.End);
        Assert.Equal(queued.End.AddDays(-7), queued.Start);
    }

    [Fact]
    public async Task Trigger_WhenRunAlreadyQueued_StillReturnsAccepted()
    {
        SetupConfiguredKey();
        _channel.Setup(x => x.TryQueueJob(It.IsAny<PriceBackfillJobRequest>())).Returns(false);

        var result = await CreateController(_validKey).Trigger(TestContext.Current.CancellationToken);

        Assert.IsType<AcceptedResult>(result);
    }
}