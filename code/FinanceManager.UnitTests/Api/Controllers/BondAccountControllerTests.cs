using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.Services;
using FinanceManager.Application.Services.Bonds;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.UnitTests.Api.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class BondAccountControllerTests
{
    private readonly Mock<IAccountRepository<BondAccount>> _mockBondAccountRepository = new();
    private readonly Mock<IBondAccountEntryRepository<BondAccountEntry>> _mockBondAccountEntryRepository = new();
    private readonly Mock<IUserPlanVerifier> _userPlanVerifier = new();
    private readonly Mock<IBondAccountCsvExportService> _bondAccountCsvExportService = new();
    private readonly BondAccountController _controller;

    public BondAccountControllerTests()
    {
        _controller = new BondAccountController(
            _mockBondAccountRepository.Object,
            _mockBondAccountEntryRepository.Object,
            _userPlanVerifier.Object,
            _bondAccountCsvExportService.Object);

        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new(ClaimTypes.NameIdentifier, "1")
        ], "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };
    }

    [Fact]
    public async Task ExportCsv_ReturnsFileResult_WithCsvContent()
    {
        var userId = 1;
        var accountId = 1;
        var startDate = new DateTime(2026, 1, 1);
        var endDate = new DateTime(2026, 1, 31);
        var csvContent = "PostingDate,ValueChange,BondDetailsId\n";
        BondAccount account = new(userId, accountId, "Bond Account", AccountLabel.Other);

        _mockBondAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _bondAccountCsvExportService
            .Setup(s => s.GetExportResults(userId, accountId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvContent);

        var result = await _controller.ExportCsv(accountId, startDate, endDate, TestContext.Current.CancellationToken);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", fileResult.ContentType);
        var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Equal(csvContent, content);
    }
}
