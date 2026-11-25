using FinanceManager.Application.MoneyFlow.SavingsRate;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class SavingsRateServiceTests
{
    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly Mock<IFinancialLabelsRepository> _labels = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<ICurrencyExchangeService> _exchange = new();
    private readonly SavingsRateService _service;

    public SavingsRateServiceTests()
    {
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { new FinancialLabel { Id = 1, Name = "Salary" } }.ToAsyncEnumerable());
        _currencies.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>())).Returns(new[] { DefaultCurrency.PLN, DefaultCurrency.USD }.ToAsyncEnumerable());
        _service = new SavingsRateService(_ledger, _labels.Object, _currencies.Object, _exchange.Object);
    }

    [Fact]
    public async Task GetSavingsRate_ConvertsInWindowSalaryAndSpending_AndExcludesInvestmentOutflows()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);
        var salary = new FinancialLabel { Id = 1, Name = "Salary" };
        var investment = new FinancialLabel { Id = 2, Name = "Investments", Classifications = [new() { Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind, Value = FinancialLabelClassificationCatalog.InvestmentValue }] };
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salary, investment }.ToAsyncEnumerable());
        _ledger.Add(DefaultCurrency.USD.Id, start.AddDays(-1), 10000m, [salary.Id])
            .Add(DefaultCurrency.USD.Id, start, 10000m, [salary.Id])
            .Add(DefaultCurrency.USD.Id, start.AddDays(1), -7000m)
            .Add(DefaultCurrency.USD.Id, start.AddDays(2), -1000m, [investment.Id]);
        _exchange.Setup(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, It.IsAny<DateTime>())).ReturnsAsync(4m);

        var result = Assert.Single(await _service.GetSavingsRate(1, DefaultCurrency.PLN, start, end).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(40000m, result.Salary);
        Assert.Equal(28000m, result.Spending);
        Assert.Equal(12000m, result.Saved);
        Assert.Equal(0.3m, result.GetPercentage());
        _exchange.Verify(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, start.AddDays(1)), Times.Once);
        _exchange.Verify(x => x.GetExchangeRateAsync(DefaultCurrency.USD, DefaultCurrency.PLN, start.AddDays(2)), Times.Never);
    }

    [Fact]
    public async Task GetSavingsRate_NegativeSalaryEntryAlsoCountsAsSpending()
    {
        var start = new DateTime(2026, 1, 1);
        var end = start.AddMonths(1);
        var salary = new FinancialLabel { Id = 1, Name = "Salary" };
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salary }.ToAsyncEnumerable());
        _ledger.Add(DefaultCurrency.PLN.Id, start, -100m, [salary.Id]);

        var result = Assert.Single(await _service.GetSavingsRate(1, DefaultCurrency.PLN, start, end).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(-100m, result.Salary);
        Assert.Equal(100m, result.Spending);
    }

    [Fact]
    public async Task GetSavingsRate_RelevantEntryWithoutExchangeRateThrows()
    {
        var start = new DateTime(2026, 1, 1);
        var end = start.AddMonths(1);
        var salary = new FinancialLabel { Id = 1, Name = "Salary" };
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salary }.ToAsyncEnumerable());
        _ledger.Add(DefaultCurrency.USD.Id, start, 1000m, [salary.Id]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.GetSavingsRate(1, DefaultCurrency.PLN, start, end).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSavingsRate_NoSalaryButSpending_ReturnsUndefinedRate()
    {
        var start = new DateTime(2026, 1, 1);
        var end = start.AddMonths(1);
        _ledger.Add(DefaultCurrency.PLN.Id, start, -100m);

        var result = Assert.Single(await _service.GetSavingsRate(1, DefaultCurrency.PLN, start, end).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0m, result.Salary);
        Assert.False(result.HasRate);
        Assert.Null(result.GetPercentage());
    }

    [Fact]
    public async Task GetSavingsRate_GuestOwnerWithUnlinkedEntries_IsCounted()
    {
        // Guest owners have negative ids and their entries are not linked to an account; PLN is currency id 0.
        var start = new DateTime(2026, 1, 1);
        var end = start.AddMonths(1);
        var salary = new FinancialLabel { Id = 1, Name = "Salary" };
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salary }.ToAsyncEnumerable());
        _ledger.Add(DefaultCurrency.PLN.Id, start, 5000m, [salary.Id], "Monthly salary")
            .Add(DefaultCurrency.PLN.Id, end, -250m);

        var result = Assert.Single(await _service.GetSavingsRate(-12, DefaultCurrency.PLN, start, end).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(5000m, result.Salary);
        Assert.Equal(250m, result.Spending);
    }
}