using FinanceManager.Api.Controllers.Admin;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceManager.Tests.Unit.Api.Controllers;

[Trait("Category", "Unit")]
public class AdminLogsControllerTests
{
    private readonly Mock<ILogEntryRepository> _repository = new();
    private readonly AdminLogsController _controller;

    public AdminLogsControllerTests()
    {
        _controller = new AdminLogsController(_repository.Object);
    }

    [Fact]
    public async Task GetLatest_RestrictsToWarningAndError()
    {
        IReadOnlyCollection<LogSeverity>? capturedLevels = null;
        _repository
            .Setup(r => r.GetLatest(5, It.IsAny<IReadOnlyCollection<LogSeverity>?>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<LogSeverity>?, CancellationToken>((_, levels, _) => capturedLevels = levels)
            .ReturnsAsync([]);

        var result = await _controller.GetLatest(5, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(capturedLevels);
        Assert.Contains(LogSeverity.Warning, capturedLevels!);
        Assert.Contains(LogSeverity.Error, capturedLevels);
        Assert.Contains(LogSeverity.Critical, capturedLevels);
        Assert.DoesNotContain(LogSeverity.Information, capturedLevels);
    }

    [Fact]
    public async Task GetLatest_CountAbove50IsBadRequest()
    {
        var result = await _controller.GetLatest(9999, TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
        _repository.Verify(
            r => r.GetLatest(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<LogSeverity>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPaged_TakeAbove200IsBadRequest()
    {
        var result = await _controller.GetPaged(0, 500, null, TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
        _repository.Verify(
            r => r.GetPaged(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<LogSeverity>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetLatest_NonPositiveCountIsBadRequest()
    {
        var result = await _controller.GetLatest(0, TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPaged_ParsesErrorFilter()
    {
        IReadOnlyCollection<LogSeverity>? capturedLevels = null;
        _repository
            .Setup(r => r.GetPaged(0, 25, It.IsAny<IReadOnlyCollection<LogSeverity>?>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, IReadOnlyCollection<LogSeverity>?, CancellationToken>((_, _, levels, _) => capturedLevels = levels)
            .ReturnsAsync(([], 0));

        await _controller.GetPaged(0, 25, "error", TestContext.Current.CancellationToken);

        Assert.NotNull(capturedLevels);
        Assert.Contains(LogSeverity.Error, capturedLevels!);
        Assert.Contains(LogSeverity.Critical, capturedLevels);
        Assert.DoesNotContain(LogSeverity.Warning, capturedLevels);
    }

    [Fact]
    public async Task GetPaged_MapsEntriesAndTotal()
    {
        var entries = new List<LogEntry>
        {
            new() { Id = 1, TimestampUtc = DateTime.UtcNow, Level = LogSeverity.Error, Category = "Cat", Message = "boom" }
        };

        _repository
            .Setup(r => r.GetPaged(0, 25, It.IsAny<IReadOnlyCollection<LogSeverity>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, 7));

        var result = await _controller.GetPaged(0, 25, null, TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedLogEntriesDto>(ok.Value);
        Assert.Equal(7, paged.TotalCount);
        Assert.Single(paged.Items);
        Assert.Equal("boom", paged.Items[0].Message);
    }

    [Fact]
    public async Task GetPaged_NegativeSkipIsBadRequest()
    {
        var result = await _controller.GetPaged(-1, 25, null, TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }
}