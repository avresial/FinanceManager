using FinanceManager.Application.MoneyFlow.Spending;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyExpenseDistributionServiceTests
{
    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly CurrencyExpenseDistributionService _service;
    private readonly DateTime _date = new(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);
    private readonly FinancialLabel _essential = CreateLabel(1, "Rent", FinancialLabelClassificationCatalog.EssentialValue);
    private readonly FinancialLabel _want = CreateLabel(2, "Dining", FinancialLabelClassificationCatalog.WantValue);

    public CurrencyExpenseDistributionServiceTests()
    {
        Mock<IFinancialLabelsRepository> labels = new();
        labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { _essential, _want }.ToAsyncEnumerable());
        _service = new CurrencyExpenseDistributionService(_ledger, labels.Object);
    }

    [Fact]
    public async Task GetExpenseDistribution_SumsOutflowsPerClassification_IgnoringInflowsAndUnlabelled()
    {
        _ledger.Add(0, _date, -100m, [_essential.Id])
            .Add(0, _date, -20.456m, [_want.Id])
            .Add(0, _date, -10m, [_want.Id])
            .Add(0, _date, 500m, [_essential.Id])
            .Add(0, _date, -999m, []);

        var result = await _service.GetExpenseDistribution(1, DefaultCurrency.PLN, _date, _date);

        Assert.Equal(2, result.Count);
        Assert.Equal(100m, result.Single(r => r.Name == FinancialLabelClassificationCatalog.EssentialValue).Value);
        Assert.Equal(30.46m, result.Single(r => r.Name == FinancialLabelClassificationCatalog.WantValue).Value);
    }

    [Fact]
    public async Task GetExpenseDistribution_GuestOwnerUnlinkedEntries_AreIncluded()
    {
        _ledger.Add(0, _date, -40m, [_essential.Id]);

        var result = await _service.GetExpenseDistribution(-7, DefaultCurrency.PLN, _date, _date);

        Assert.Equal(40m, Assert.Single(result).Value);
    }

    [Fact]
    public async Task GetExpenseDistribution_UnresolvedClassification_IsSkipped()
    {
        _ledger.Add(0, _date, -10m, [_essential.Id, _want.Id]);

        Assert.Empty(await _service.GetExpenseDistribution(1, DefaultCurrency.PLN, _date, _date));
    }

    private static FinancialLabel CreateLabel(int id, string name, string value) =>
        new()
        {
            Id = id,
            Name = name,
            Classifications = [new FinancialLabelClassification { Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind, Value = value }]
        };
}