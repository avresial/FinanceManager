using FinanceManager.Api.Features.Dashboard.Controllers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.Tests.Unit.Api.Features.Dashboard.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class DashboardControllerTests
{
    private const int _testUserId = 1;
    private readonly DateTime _start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _end = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string _startText = "2024-01-01T00:00:00Z";
    private const string _endText = "2024-02-01T00:00:00Z";

    private readonly Mock<IDashboardQueryService> _dashboardQueryService = new();
    private readonly DashboardController _controller;

    public DashboardControllerTests()
    {
        _controller = new DashboardController(_dashboardQueryService.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, _testUserId.ToString())], "mock"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetOverview_ReturnsOk_ForAuthenticatedUser()
    {
        // Arrange
        var dto = new DashboardOverviewDto { UserId = _testUserId, CurrencyId = 0, StartDate = _start, EndDate = _end };
        _dashboardQueryService.Setup(s => s.GetOverview(_testUserId, 0, _start, _end, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetOverview(_testUserId, 0, _startText, _endText, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(dto, okResult.Value);
    }

    [Fact]
    public async Task GetOverview_ReturnsNotFound_WhenServiceReturnsNull()
    {
        // Arrange
        _dashboardQueryService.Setup(s => s.GetOverview(_testUserId, 999, _start, _end, It.IsAny<CancellationToken>())).ReturnsAsync((DashboardOverviewDto?)null);

        // Act
        var result = await _controller.GetOverview(_testUserId, 999, _startText, _endText, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetOverview_ReturnsForbid_ForDifferentUser()
    {
        // Act
        var result = await _controller.GetOverview(_testUserId + 1, 0, _startText, _endText, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result);
        _dashboardQueryService.Verify(s => s.GetOverview(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOverview_DateOnlyRange_CoversWholeUtcDays()
    {
        // Act
        await _controller.GetOverview(_testUserId, 0, "2024-01-01", "2024-01-31", TestContext.Current.CancellationToken);

        // Assert
        var expectedEnd = new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
        _dashboardQueryService.Verify(s => s.GetOverview(_testUserId, 0,
            It.Is<DateTime>(d => d == _start && d.Kind == DateTimeKind.Utc),
            It.Is<DateTime>(d => d == expectedEnd && d.Kind == DateTimeKind.Utc),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOverview_TimestampWithOffset_IsNormalizedToUtc()
    {
        // Act
        await _controller.GetOverview(_testUserId, 0, "2024-01-01T02:00:00+02:00", "2024-01-31T10:30:00-01:00", TestContext.Current.CancellationToken);

        // Assert
        var expectedEnd = new DateTime(2024, 1, 31, 11, 30, 0, DateTimeKind.Utc);
        _dashboardQueryService.Verify(s => s.GetOverview(_testUserId, 0,
            It.Is<DateTime>(d => d == _start && d.Kind == DateTimeKind.Utc),
            It.Is<DateTime>(d => d == expectedEnd && d.Kind == DateTimeKind.Utc),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOverview_LastRepresentableDay_DoesNotOverflow()
    {
        // Act
        await _controller.GetOverview(_testUserId, 0, "9999-12-31", "9999-12-31", TestContext.Current.CancellationToken);

        // Assert
        _dashboardQueryService.Verify(s => s.GetOverview(_testUserId, 0,
            It.IsAny<DateTime>(), DateTime.MaxValue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("not-a-date", _endText, "start")]
    [InlineData("12:30", _endText, "start")]
    [InlineData("01/02/2024", _endText, "start")]
    [InlineData(_startText, "2024-02-30", "end")]
    [InlineData(_startText, "10000-01-01", "end")]
    [InlineData(_startText, "9999-12-31T23:00:00-05:00", "end")]
    [InlineData("0001-01-01T00:00:00+01:00", _endText, "start")]
    [InlineData("2024-02-02", "2024-02-01", "end")]
    public async Task GetOverview_InvalidRange_ReturnsValidationProblem(string start, string end, string invalidField)
    {
        // Act
        var result = await _controller.GetOverview(_testUserId, 0, start, end, TestContext.Current.CancellationToken);

        // Assert
        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Contains(invalidField, problem.Errors.Keys);
        _dashboardQueryService.Verify(s => s.GetOverview(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}