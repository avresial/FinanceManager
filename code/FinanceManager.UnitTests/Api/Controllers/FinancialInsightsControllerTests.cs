using FinanceManager.Api.Controllers;
using FinanceManager.Application.Services.FinancialInsights;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.UnitTests.Api.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class FinancialInsightsControllerTests
{
    private const int TestUserId = 1;

    private readonly Mock<IFinancialInsightsRepository> _mockRepository;
    private readonly Mock<IFinancialInsightsAiGenerator> _mockGenerator;
    private readonly FinancialInsightsController _controller;

    public FinancialInsightsControllerTests()
    {
        _mockRepository = new Mock<IFinancialInsightsRepository>();
        _mockGenerator = new Mock<IFinancialInsightsAiGenerator>();
        _controller = new FinancialInsightsController(_mockRepository.Object, _mockGenerator.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, TestUserId.ToString())], "mock"));
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
            new() { Id = 1, UserId = TestUserId, Title = "Title", Message = "Message", Tags = "tag1,tag2", CreatedAt = DateTime.UtcNow }
        };

        _mockRepository
            .Setup(repo => repo.GetLatestByUser(TestUserId, 3, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insights);

        var result = await _controller.GetLatest(3, 7, TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<FinancialInsight>>(okResult.Value);
        Assert.Single(returnValue);
    }
}