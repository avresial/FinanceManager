using FinanceManager.Api.Features.Insights.Controllers;
using FinanceManager.Api.Features.Insights.Services;
using FinanceManager.Application.Insights.Generation;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Tests.Unit.Shared.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace FinanceManager.Tests.Unit.Api.Features.Insights.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class FinancialInsightsControllerTests
{
    private const int _testUserId = 1;
    private static readonly DateTime _utcNow = new(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IFinancialInsightsRepository> _mockRepository;
    private readonly Mock<IFinancialInsightsAiGenerator> _mockGenerator;
    private readonly Mock<IInsightsGenerationChannel> _mockGenerationChannel;
    private readonly FinancialInsightsController _controller;

    public FinancialInsightsControllerTests()
    {
        _mockRepository = new Mock<IFinancialInsightsRepository>();
        _mockGenerator = new Mock<IFinancialInsightsAiGenerator>();
        _mockGenerationChannel = new Mock<IInsightsGenerationChannel>();
        _controller = new FinancialInsightsController(
            _mockRepository.Object,
            _mockGenerator.Object,
            _mockGenerationChannel.Object,
            new FakeDateTimeProvider(_utcNow),
            NullLogger<FinancialInsightsController>.Instance);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, _testUserId.ToString())], "mock"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetLatest_ReturnsList()
    {
        var insights = new List<FinancialInsight>
        {
            new() { Id = 1, UserId = _testUserId, Title = "Title", Message = "Message", Tags = "tag1,tag2", CreatedAt = _utcNow }
        };

        _mockRepository
            .Setup(repo => repo.GetLatestByUser(_testUserId, 3, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insights);

        var result = await _controller.GetLatest(3, 7, TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<FinancialInsight>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetLatest_QueuesRegeneration_WhenNewestInsightIsOlderThanSevenDays()
    {
        SetupLatestInsights(BuildInsight(_utcNow.AddDays(-7).AddMinutes(-1)));

        await _controller.GetLatest(3, cancellationToken: TestContext.Current.CancellationToken);

        _mockGenerationChannel.Verify(channel => channel.QueueUser(_testUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLatest_QueuesRegeneration_WhenUserHasNoInsights()
    {
        SetupLatestInsights();

        await _controller.GetLatest(3, cancellationToken: TestContext.Current.CancellationToken);

        _mockGenerationChannel.Verify(channel => channel.QueueUser(_testUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLatest_DoesNotQueueRegeneration_WhenNewestInsightIsWithinSevenDays()
    {
        SetupLatestInsights(BuildInsight(_utcNow.AddDays(-6)));

        await _controller.GetLatest(3, cancellationToken: TestContext.Current.CancellationToken);

        _mockGenerationChannel.Verify(channel => channel.QueueUser(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLatest_DoesNotQueueRegeneration_ForAccountScopedRead()
    {
        _mockRepository
            .Setup(repo => repo.GetLatestByUser(_testUserId, 3, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _controller.GetLatest(3, 7, TestContext.Current.CancellationToken);

        _mockGenerationChannel.Verify(channel => channel.QueueUser(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLatest_ReturnsInsights_WhenQueueingFails()
    {
        SetupLatestInsights(BuildInsight(_utcNow.AddDays(-30)));
        _mockGenerationChannel
            .Setup(channel => channel.QueueUser(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("channel unavailable"));

        var result = await _controller.GetLatest(3, cancellationToken: TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<FinancialInsight>>(okResult.Value);
        Assert.Single(returnValue);
    }

    private void SetupLatestInsights(params FinancialInsight[] insights) =>
        _mockRepository
            .Setup(repo => repo.GetLatestByUser(_testUserId, 3, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. insights]);

    private static FinancialInsight BuildInsight(DateTime createdAt) =>
        new() { Id = 1, UserId = _testUserId, Title = "Title", Message = "Message", Tags = "tag1,tag2", CreatedAt = createdAt };
}