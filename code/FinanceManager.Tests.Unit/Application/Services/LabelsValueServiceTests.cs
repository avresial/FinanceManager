using FinanceManager.Application.MoneyFlow.LabelsValue;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class LabelsValueServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
    private readonly LabelsValueService _labelsValueService;

    public LabelsValueServiceTests()
    {
        _labelsValueService = new LabelsValueService(_ledger, _financialLabelsRepositoryMock.Object);
    }

    [Fact]
    public async Task GetLabelsValue_ReturnsLabelsValue()
    {
        var label = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { label }.ToAsyncEnumerable());
        _ledger.Add(0, _startDate, 500, [label.Id]);

        var result = await _labelsValueService.GetLabelsValue(1, _startDate, _endDate);

        Assert.Equal(500, result.First(x => x.Name == label.Name).Value);
    }

    [Fact]
    public async Task GetLabelsValue_ListsEveryLabelOrderedByValue_AndCountsMultiLabelEntriesForEach()
    {
        var salary = new FinancialLabel { Id = 1, Name = "Salary" };
        var rent = new FinancialLabel { Id = 2, Name = "Rent" };
        var unused = new FinancialLabel { Id = 3, Name = "Unused" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { unused, rent, salary }.ToAsyncEnumerable());
        _ledger.Add(0, _startDate, 5000, [salary.Id])
            .Add(0, _startDate.AddDays(1), -1000, [rent.Id, salary.Id])
            .Add(0, _startDate.AddDays(2), -50)
            .Add(0, _endDate.AddYears(1), 9999, [salary.Id]);

        var result = await _labelsValueService.GetLabelsValue(-3, _startDate, _endDate);

        Assert.Equal(["Salary", "Unused", "Rent"], result.Select(x => x.Name));
        Assert.Equal([4000m, 0m, -1000m], result.Select(x => x.Value));
    }
}