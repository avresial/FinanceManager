using FinanceManager.Application.MoneyFlow.SavingsRate;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class SavingsRateServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _accounts = new();
    private readonly Mock<IFinancialLabelsRepository> _labels = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<ICurrencyExchangeService> _exchange = new();
    private readonly SavingsRateService _service;

    public SavingsRateServiceTests()
    {
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { new FinancialLabel { Id = 1, Name = "Salary" } }.ToAsyncEnumerable());
        _currencies.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>())).Returns(new[] { DefaultCurrency.PLN, DefaultCurrency.USD }.ToAsyncEnumerable());
        _accounts.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _service = new SavingsRateService(_accounts.Object, _labels.Object, _currencies.Object, _exchange.Object);
    }

    [Fact]
    public async Task GetSavingsRate_ConvertsInWindowSalaryAndSpending_AndExcludesInvestmentOutflows()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);
        var salary = new FinancialLabel { Id = 1, Name = "Salary" };
        var investment = new FinancialLabel { Id = 2, Name = "Investments", Classifications = [new() { Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind, Value = FinancialLabelClassificationCatalog.InvestmentValue }] };
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salary, investment }.ToAsyncEnumerable());
        var account = new CurrencyAccount(1, 1, "USD", AccountLabel.Cash, DefaultCurrency.USD.Id);
        account.Add(new CurrencyAccountEntry(1, 1, start.AddDays(-1), 10000m, 10000m) { Labels = [salary] }, false);
        account.Add(new CurrencyAccountEntry(1, 2, start, 10000m, 10000m) { Labels = [salary] }, false);
        account.Add(new CurrencyAccountEntry(1, 3, start.AddDays(1), 3000m, -7000m) { Labels = [] }, false);
        account.Add(new CurrencyAccountEntry(1, 4, start.AddDays(2), 2000m, -1000m) { Labels = [investment] }, false);
        _accounts.Setup(x => x.GetAccounts<CurrencyAccount>(1, start, end)).Returns(new[] { account }.ToAsyncEnumerable());
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
        var account = new CurrencyAccount(1, 1, "PLN", AccountLabel.Cash, DefaultCurrency.PLN.Id);
        account.Add(new CurrencyAccountEntry(1, 1, start, 1000m, -100m) { Labels = [salary] }, false);
        _accounts.Setup(x => x.GetAccounts<CurrencyAccount>(1, start, end)).Returns(new[] { account }.ToAsyncEnumerable());

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
        var account = new CurrencyAccount(1, 1, "USD", AccountLabel.Cash, DefaultCurrency.USD.Id);
        account.Add(new CurrencyAccountEntry(1, 1, start, 1000m, 1000m) { Labels = [salary] }, false);
        _accounts.Setup(x => x.GetAccounts<CurrencyAccount>(1, start, end)).Returns(new[] { account }.ToAsyncEnumerable());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _service.GetSavingsRate(1, DefaultCurrency.PLN, start, end).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSavingsRate_NoSalaryButSpending_ReturnsUndefinedRate()
    {
        var start = new DateTime(2026, 1, 1);
        var end = start.AddMonths(1);
        var account = new CurrencyAccount(1, 1, "PLN", AccountLabel.Cash, DefaultCurrency.PLN.Id);
        account.Add(new CurrencyAccountEntry(1, 1, start, 100m, -100m) { Labels = [] }, false);
        _accounts.Setup(x => x.GetAccounts<CurrencyAccount>(1, start, end)).Returns(new[] { account }.ToAsyncEnumerable());

        var result = Assert.Single(await _service.GetSavingsRate(1, DefaultCurrency.PLN, start, end).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0m, result.Salary);
        Assert.False(result.HasRate);
        Assert.Null(result.GetPercentage());
    }
}