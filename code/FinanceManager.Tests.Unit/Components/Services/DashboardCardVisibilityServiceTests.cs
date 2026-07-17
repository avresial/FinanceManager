using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Services;

[Trait("Category", "Unit")]
public class DashboardCardVisibilityServiceTests
{
    private const string _expectedKey = "dashboard-card-visibility:7";

    private static Mock<ILoginService> LoggedInAs(int userId)
    {
        var loginService = new Mock<ILoginService>();
        loginService.Setup(s => s.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = userId,
            UserName = "tester",
            Password = "password",
            UserRole = UserRole.User,
        });
        return loginService;
    }

    [Fact]
    public async Task EnsureLoadedAsync_HydratesHiddenCardsFromSnapshot()
    {
        var snapshotService = new Mock<ISnapshotService>();
        snapshotService
            .Setup(s => s.GetAsync<DashboardCardVisibilitySnapshot>(_expectedKey))
            .ReturnsAsync(new DashboardCardVisibilitySnapshot { HiddenCardIds = [DashboardCards.Liabilities] });

        var service = new DashboardCardVisibilityService(snapshotService.Object, LoggedInAs(7).Object,
            NullLogger<DashboardCardVisibilityService>.Instance);

        await service.EnsureLoadedAsync();

        Assert.True(service.IsHidden(DashboardCards.Liabilities));
        Assert.False(service.IsHidden(DashboardCards.Assets));
    }

    [Fact]
    public async Task EnsureLoadedAsync_WithNoSnapshot_TreatsAllCardsAsVisible()
    {
        var snapshotService = new Mock<ISnapshotService>();
        snapshotService
            .Setup(s => s.GetAsync<DashboardCardVisibilitySnapshot>(It.IsAny<string>()))
            .ReturnsAsync((DashboardCardVisibilitySnapshot?)null);

        var service = new DashboardCardVisibilityService(snapshotService.Object, LoggedInAs(7).Object,
            NullLogger<DashboardCardVisibilityService>.Instance);

        await service.EnsureLoadedAsync();

        Assert.False(service.IsHidden(DashboardCards.Liabilities));
    }

    [Fact]
    public async Task SetHiddenAsync_HidingCard_PersistsUnderPerUserKey()
    {
        var snapshotService = new Mock<ISnapshotService>();
        snapshotService
            .Setup(s => s.GetAsync<DashboardCardVisibilitySnapshot>(It.IsAny<string>()))
            .ReturnsAsync((DashboardCardVisibilitySnapshot?)null);

        var service = new DashboardCardVisibilityService(snapshotService.Object, LoggedInAs(7).Object,
            NullLogger<DashboardCardVisibilityService>.Instance);

        await service.SetHiddenAsync(DashboardCards.Liabilities, true);

        Assert.True(service.IsHidden(DashboardCards.Liabilities));
        snapshotService.Verify(s => s.SetAsync(_expectedKey,
            It.Is<DashboardCardVisibilitySnapshot>(x => x.HiddenCardIds.Contains(DashboardCards.Liabilities))), Times.Once);
    }

    [Fact]
    public async Task SetHiddenAsync_ShowingPreviouslyHiddenCard_RemovesIt()
    {
        var snapshotService = new Mock<ISnapshotService>();
        snapshotService
            .Setup(s => s.GetAsync<DashboardCardVisibilitySnapshot>(It.IsAny<string>()))
            .ReturnsAsync(new DashboardCardVisibilitySnapshot { HiddenCardIds = [DashboardCards.Liabilities] });

        var service = new DashboardCardVisibilityService(snapshotService.Object, LoggedInAs(7).Object,
            NullLogger<DashboardCardVisibilityService>.Instance);

        await service.SetHiddenAsync(DashboardCards.Liabilities, false);

        Assert.False(service.IsHidden(DashboardCards.Liabilities));
        snapshotService.Verify(s => s.SetAsync(_expectedKey,
            It.Is<DashboardCardVisibilitySnapshot>(x => !x.HiddenCardIds.Contains(DashboardCards.Liabilities))), Times.Once);
    }

    [Fact]
    public async Task SetHiddenAsync_WhenStateUnchanged_DoesNotPersistAgain()
    {
        var snapshotService = new Mock<ISnapshotService>();
        snapshotService
            .Setup(s => s.GetAsync<DashboardCardVisibilitySnapshot>(It.IsAny<string>()))
            .ReturnsAsync((DashboardCardVisibilitySnapshot?)null);

        var service = new DashboardCardVisibilityService(snapshotService.Object, LoggedInAs(7).Object,
            NullLogger<DashboardCardVisibilityService>.Instance);

        // Card is already visible; asking to show it again is a no-op.
        await service.SetHiddenAsync(DashboardCards.Liabilities, false);

        snapshotService.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<DashboardCardVisibilitySnapshot>()), Times.Never);
    }
}