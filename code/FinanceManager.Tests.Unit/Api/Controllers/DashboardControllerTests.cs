using FinanceManager.Api.Controllers;
using FinanceManager.Domain.Dtos.Dashboard;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.Tests.Unit.Api.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class DashboardControllerTests
{
    private const int _testUserId = 1;
    private readonly DateTime _start = new(2024, 1, 1);
    private readonly DateTime _end = new(2024, 2, 1);

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
        var result = await _controller.GetOverview(_testUserId, 0, _start, _end, TestContext.Current.CancellationToken);

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
        var result = await _controller.GetOverview(_testUserId, 999, _start, _end, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetOverview_ReturnsForbid_ForDifferentUser()
    {
        // Act
        var result = await _controller.GetOverview(_testUserId + 1, 0, _start, _end, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result);
        _dashboardQueryService.Verify(s => s.GetOverview(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}