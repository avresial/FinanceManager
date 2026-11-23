using FinanceManager.Application.MoneyFlow.Spending;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Labels.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyEssentialSpendingServiceTests
{
    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly Mock<IFinancialLabelsRepository> _labelsMock = new();
    private readonly CurrencyEssentialSpendingService _service;
    private readonly DateTime _date = new(2026, 3, 10);
    private readonly FinancialLabel _essential = CreateLabel(1, "Rent", FinancialLabelClassificationCatalog.EssentialValue);
    private readonly FinancialLabel _want = CreateLabel(2, "Dining", FinancialLabelClassificationCatalog.WantValue);

    public CurrencyEssentialSpendingServiceTests()
    {
        _labelsMock.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { _essential, _want }.ToAsyncEnumerable());
        _service = new CurrencyEssentialSpendingService(_ledger, _labelsMock.Object);
    }

    [Fact]
    public async Task GetEssentialSpending_IncludesOnlyResolvedEssentialOutflows()
    {
        _ledger.Add(0, _date, -100m, [_essential.Id])
            .Add(0, _date, -50m, [_essential.Id, _want.Id, _essential.Id])
            .Add(0, _date, -10m, [_essential.Id, _want.Id])
            .Add(0, _date, 100m, [_essential.Id]);

        var result = await _service.GetEssentialSpending(1, DefaultCurrency.PLN, _date, _date);

        Assert.Single(result);
        Assert.Equal(-150m, result[0].Value);
    }

    [Fact]
    public async Task GetEssentialSpending_TieResolution_ExcludesEntry()
    {
        _ledger.Add(0, _date, -10m, [_essential.Id, _want.Id]);

        var result = await _service.GetEssentialSpending(1, DefaultCurrency.PLN, _date, _date);

        Assert.Single(result);
        Assert.Equal(0m, result[0].Value);
    }

    [Fact]
    public async Task GetEssentialSpending_GuestOwnerUnlinkedEntries_FillsEveryDayOfTheWindow()
    {
        _ledger.Add(0, _date.AddDays(1), -30m, [_essential.Id]);

        var result = await _service.GetEssentialSpending(-5, DefaultCurrency.PLN, _date, _date.AddDays(2));

        Assert.Equal(3, result.Count);
        Assert.Equal(-30m, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetEssentialSpending_NoEntriesInWindow_ReturnsEmpty()
    {
        _ledger.Add(0, _date.AddDays(-30), -30m, [_essential.Id]);

        Assert.Empty(await _service.GetEssentialSpending(1, DefaultCurrency.PLN, _date, _date.AddDays(2)));
    }

    private static FinancialLabel CreateLabel(int id, string name, string value) =>
        new()
        {
            Id = id,
            Name = name,
            Classifications =
            [
                new FinancialLabelClassification
                {
                    Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind,
                    Value = value
                }
            ]
        };
}