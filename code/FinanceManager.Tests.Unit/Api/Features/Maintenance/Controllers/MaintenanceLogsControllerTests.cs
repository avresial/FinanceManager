using FinanceManager.Api.Features.Maintenance.Controllers;
using FinanceManager.Application.Shared.Maintenance;
using FinanceManager.Domain.Administration.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Api.Features.Maintenance.Controllers;

[Trait("Category", "Unit")]
public class MaintenanceLogsControllerTests
{
    private const string _validKey = "fmk_test-maintenance-key";

    private readonly Mock<IMaintenanceKeyService> _keyService = new();
    private readonly Mock<ILogEntryRepository> _repository = new();

    private MaintenanceLogsController CreateController(string? providedKey)
    {
        var httpContext = new DefaultHttpContext();
        if (providedKey is not null)
            httpContext.Request.Headers[PriceBackfillController.ApiKeyHeader] = providedKey;

        return new MaintenanceLogsController(
            _keyService.Object,
            _repository.Object,
            NullLogger<MaintenanceLogsController>.Instance)
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
    public async Task Get_WhenNoMaintenanceKeyIsConfigured_ReturnsNotFoundWithoutReadingLogs()
    {
        _keyService.Setup(x => x.IsConfiguredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateController(_validKey).Get(cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        _repository.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyCollection<LogSeverity>?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    public async Task Get_WhenKeyIsMissingOrInvalid_ReturnsUnauthorizedWithoutReadingLogs(string? providedKey)
    {
        SetupConfiguredKey();

        var result = await CreateController(providedKey).Get(cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result);
        _repository.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyCollection<LogSeverity>?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Get_WithValidKey_PassesFiltersAndMapsPagedResults()
    {
        SetupConfiguredKey();
        var fromUtc = new DateTime(2026, 8, 27, 4, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddHours(1);
        var entry = new LogEntry
        {
            Id = 42,
            TimestampUtc = fromUtc.AddMinutes(5),
            Level = LogSeverity.Error,
            Category = "Provider",
            Message = "Provider failed",
            Exception = "TimeoutException",
            EventId = 7,
            EventName = "ProviderFailure"
        };
        _repository
            .Setup(x => x.GetPaged(
                10,
                5,
                It.IsAny<IReadOnlyCollection<LogSeverity>?>(),
                fromUtc,
                toUtc,
                "provider",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(([entry], 11));

        var result = await CreateController(_validKey).Get(
            skip: 10,
            take: 5,
            level: "error",
            fromUtc: fromUtc,
            toUtc: toUtc,
            search: "provider",
            cancellationToken: TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedLogEntriesDto>(ok.Value);
        Assert.Equal(11, paged.TotalCount);
        var mapped = Assert.Single(paged.Items);
        Assert.Equal(entry.Id, mapped.Id);
        Assert.Equal(entry.Exception, mapped.Exception);
        Assert.Equal(entry.EventName, mapped.EventName);
        _repository.Verify(
            x => x.GetPaged(
                10,
                5,
                It.Is<IReadOnlyCollection<LogSeverity>?>(levels =>
                    levels != null && levels.Count == 1 && levels.Contains(LogSeverity.Error)),
                fromUtc,
                toUtc,
                "provider",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(-1, 25, null)]
    [InlineData(0, 0, null)]
    [InlineData(0, 201, null)]
    [InlineData(0, 25, "verbose")]
    public async Task Get_WhenQueryIsInvalid_ReturnsBadRequest(int skip, int take, string? level)
    {
        SetupConfiguredKey();

        var result = await CreateController(_validKey).Get(
            skip: skip,
            take: take,
            level: level,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        _repository.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyCollection<LogSeverity>?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Get_WhenTimeRangeIsReversed_ReturnsBadRequest()
    {
        SetupConfiguredKey();

        var result = await CreateController(_validKey).Get(
            fromUtc: DateTime.UtcNow,
            toUtc: DateTime.UtcNow.AddMinutes(-1),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Get_WhenSearchIsTooLong_ReturnsBadRequest()
    {
        SetupConfiguredKey();

        var result = await CreateController(_validKey).Get(
            search: new string('x', 257),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}