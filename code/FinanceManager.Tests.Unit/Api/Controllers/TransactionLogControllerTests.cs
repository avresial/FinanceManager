using FinanceManager.Api.Controllers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.Tests.Unit.Api.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class TransactionLogControllerTests
{
    private const int _testUserId = 1;

    private readonly Mock<ITransactionLogService> _transactionLogService = new();
    private readonly TransactionLogController _controller;

    public TransactionLogControllerTests()
    {
        _controller = new TransactionLogController(_transactionLogService.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, _testUserId.ToString())], "mock"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task Get_ReturnsOk_ForAuthenticatedUser()
    {
        // Arrange
        List<TransactionLogEntryDto> transactions =
        [
            new(10, "Bank", AccountType.Currency, 1, new DateTime(2024, 3, 5), -50, "Groceries"),
        ];
        _transactionLogService.Setup(s => s.GetLastTransactions(_testUserId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        var result = await _controller.Get(_testUserId, 10, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(transactions, okResult.Value);
    }

    [Fact]
    public async Task Get_ReturnsForbid_WhenRequestingAnotherUsersData()
    {
        // Act
        var result = await _controller.Get(_testUserId + 1, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result);
        _transactionLogService.Verify(s => s.GetLastTransactions(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}