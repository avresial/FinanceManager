using FinanceManager.Application.MoneyFlow.LabelsValue;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class LabelsValueServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly LabelsValueService _labelsValueService;
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();

    public LabelsValueServiceTests()
    {
        _labelsValueService = new LabelsValueService(_financialAccountRepositoryMock.Object, _financialLabelsRepositoryMock.Object);
    }

    [Fact]
    public async Task GetLabelsValue_ReturnsLabelsValue()
    {
        // Arrange
        var userId = 1;
        var label = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { label }.ToAsyncEnumerable());

        var account = new CurrencyAccount(userId, 1, "Currency Account 1", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _startDate, 500, 500) { Labels = [label] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new[] { account }.ToAsyncEnumerable());

        // Act
        var result = await _labelsValueService.GetLabelsValue(userId, _startDate, _endDate);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(500, result.First(x => x.Name == label.Name).Value);
    }
}