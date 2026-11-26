using FinanceManager.Application.MoneyFlow.InvestmentRate;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentRateServiceTests
{
    private readonly DateTime _startDate = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _endDate = DateTime.UtcNow;

    private readonly InvestmentRateService _investmentRateService;
    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly Mock<IFinancialLabelsRepository> _financialLabelsRepositoryMock = new();
    private readonly Mock<IInvestmentTransactionRepository> _investmentTransactionRepositoryMock = new();
    private readonly Mock<ICurrencyRepository> _currencyRepositoryMock = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeServiceMock = new();

    public InvestmentRateServiceTests()
    {
        _investmentTransactionRepositoryMock
            .Setup(x => x.GetByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _investmentRateService = new InvestmentRateService(
            _ledger,
            _financialLabelsRepositoryMock.Object,
            _investmentTransactionRepositoryMock.Object,
            _currencyRepositoryMock.Object,
            _currencyExchangeServiceMock.Object);
    }

    [Fact]
    public async Task GetInvestmentRate_ReturnsInvestmentRate()
    {
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        _ledger.Add(DefaultCurrency.PLN.Id, _startDate, 1000, [salaryLabel.Id]);
        _investmentTransactionRepositoryMock
            .Setup(x => x.GetByUser(userId, DateOnly.FromDateTime(_startDate), DateOnly.FromDateTime(_endDate), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Transaction(userId, _startDate, InvestmentTransactionType.Buy, 2m, 5m)]);

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, DefaultCurrency.PLN, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(1000, result.First().Salary);
        Assert.Equal(10, result.First().InvestmentsChange);
    }

    [Fact]
    public async Task GetInvestmentRate_SalaryPaidInEarlierMonth_ReportsZeroSalaryAndNoRate()
    {
        // The salary for the current month has not arrived yet — the last one landed in the previous
        // month — but investments were made this month. The earlier salary must not be carried into
        // this month: the month reports 0 salary, the real investments, and no rate at all.
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        var monthStart = new DateTime(_endDate.Year, _endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var salaryDate = monthStart.AddMonths(-1).AddDays(25);

        _ledger.Add(DefaultCurrency.PLN.Id, salaryDate, 9456.88m, [salaryLabel.Id]);

        _investmentTransactionRepositoryMock
            .Setup(x => x.GetByUser(userId, DateOnly.FromDateTime(monthStart), DateOnly.FromDateTime(_endDate), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Transaction(userId, monthStart, InvestmentTransactionType.Buy, 5m, 100m)]);

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, DefaultCurrency.PLN, monthStart, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var rate = Assert.Single(result);
        Assert.Equal(0, rate.Salary);
        Assert.Equal(500, rate.InvestmentsChange);
        Assert.False(rate.HasRate);
        Assert.Null(rate.GetPercentage());
    }

    [Fact]
    public async Task GetInvestmentRate_NoSalaryFound_StillReportsInvestmentChange()
    {
        // No salary anywhere (window or lookback), but the investments did change — the change must
        // still be reported instead of the month silently disappearing from the series.
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        var monthStart = new DateTime(_endDate.Year, _endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _investmentTransactionRepositoryMock
            .Setup(x => x.GetByUser(userId, DateOnly.FromDateTime(monthStart), DateOnly.FromDateTime(_endDate), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Transaction(userId, monthStart, InvestmentTransactionType.Buy, 1m, 250m)]);

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, DefaultCurrency.PLN, monthStart, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var rate = Assert.Single(result);
        Assert.Equal(0, rate.Salary);
        Assert.Equal(250, rate.InvestmentsChange);
        Assert.False(rate.HasRate);
        Assert.Null(rate.GetPercentage());
    }

    [Fact]
    public async Task GetInvestmentRate_MonthWithNoActivity_ReturnsEmpty()
    {
        // The user's most recent transactions are months old: the queried month has no salary and no
        // investment purchases. Nothing happened, so there is nothing to report for the month.
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        var monthStart = new DateTime(_endDate.Year, _endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var salaryDate = monthStart.AddMonths(-3).AddDays(9);

        _ledger.Add(DefaultCurrency.PLN.Id, salaryDate, 1000, [salaryLabel.Id]);

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, DefaultCurrency.PLN, monthStart, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetInvestmentRate_NoSalaryAndNoInvestmentChange_ReturnsEmpty()
    {
        // Arrange
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        // Act
        var result = await _investmentRateService.GetInvestmentRate(userId, DefaultCurrency.PLN, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetInvestmentRate_BuysSellsAndFees_ReturnsNetContributions()
    {
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());

        _ledger.Add(DefaultCurrency.PLN.Id, _startDate, 1000m, [salaryLabel.Id]);
        _investmentTransactionRepositoryMock
            .Setup(x => x.GetByUser(userId, DateOnly.FromDateTime(_startDate), DateOnly.FromDateTime(_endDate), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Transaction(userId, _startDate, InvestmentTransactionType.Buy, 10m, 100m, 10m),
                Transaction(userId, _endDate, InvestmentTransactionType.Sell, 2m, 100m, 5m),
            ]);

        var result = await _investmentRateService.GetInvestmentRate(userId, DefaultCurrency.PLN, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        var rate = Assert.Single(result);
        Assert.Equal(815m, rate.InvestmentsChange);
        Assert.Equal(0.815m, rate.GetPercentage());
    }

    [Fact]
    public async Task GetInvestmentRate_ConvertsTransactionCurrency()
    {
        var userId = 1;
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());
        _investmentTransactionRepositoryMock
            .Setup(x => x.GetByUser(userId, DateOnly.FromDateTime(_startDate), DateOnly.FromDateTime(_endDate), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Transaction(userId, _startDate, InvestmentTransactionType.Buy, 10m, 10m, 2m, "USD")]);
        _currencyRepositoryMock.Setup(x => x.GetOrAdd("USD", "USD", It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.USD);
        _currencyExchangeServiceMock
            .Setup(x => x.GetExchangeRateAsync(DefaultCurrency.USD, It.Is<Currency>(c => c.ShortName == "PLN"), _startDate.Date))
            .ReturnsAsync(4m);

        var result = await _investmentRateService.GetInvestmentRate(userId, DefaultCurrency.PLN, _startDate, _endDate).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        var rate = Assert.Single(result);
        Assert.Equal(408m, rate.InvestmentsChange);
    }

    [Fact]
    public async Task GetInvestmentRate_CountsSalaryOnWindowEndAndIgnoresOtherEntries()
    {
        // The salary posted exactly on the window end counts; unlabelled income and a salary after the window do not.
        var salaryLabel = new FinancialLabel { Id = 1, Name = "Salary" };
        _financialLabelsRepositoryMock.Setup(repo => repo.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { salaryLabel }.ToAsyncEnumerable());
        var end = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);
        _ledger.Add(DefaultCurrency.PLN.Id, end, 1000m, [salaryLabel.Id])
            .Add(DefaultCurrency.PLN.Id, end.AddDays(-1), 700m)
            .Add(DefaultCurrency.PLN.Id, end.AddDays(1), 5000m, [salaryLabel.Id]);

        var result = await _investmentRateService.GetInvestmentRate(-7, DefaultCurrency.PLN, end.AddDays(-27), end).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1000m, Assert.Single(result).Salary);
    }

    private static InvestmentTransaction Transaction(
        int userId,
        DateTime date,
        InvestmentTransactionType type,
        decimal quantity,
        decimal unitPrice,
        decimal fee = 0m,
        string currency = "PLN") => new()
        {
            UserId = userId,
            Type = type,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Fee = fee,
            Currency = currency,
            TradeDate = DateOnly.FromDateTime(date),
        };
}