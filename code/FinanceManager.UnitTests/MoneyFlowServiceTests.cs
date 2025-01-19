using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests
{
    public class MoneyFlowServiceTests
    {
        private readonly DateTime startDate = new(2020, 1, 1);
        private readonly DateTime endDate = new(2020, 1, 31);
        private readonly decimal totalAssetsValue = 0;

        private readonly MoneyFlowService _moneyFlowService;
        private readonly Mock<IFinancalAccountRepository> _financalAccountRepositoryMock = new();
        private readonly Mock<IStockRepository> _stockRepository = new();
        private readonly List<BankAccount> _bankAccounts;
        private readonly List<InvestmentAccount> _investmentAccountAccounts;

        public MoneyFlowServiceTests()
        {

            BankAccount bankAccount1 = new(1, "testBank1", AccountType.Cash);
            bankAccount1.Add(new BankAccountEntry(1, startDate, 10, 10));
            bankAccount1.Add(new BankAccountEntry(2, startDate.AddDays(1), 20, 10));

            BankAccount bankAccount2 = new(2, "testBank2", AccountType.Cash);
            bankAccount2.Add(new BankAccountEntry(1, endDate, 10, 10));

            _bankAccounts = [bankAccount1, bankAccount2];
            _financalAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(startDate, endDate))
                                            .Returns(_bankAccounts);

            InvestmentAccount investmentAccount1 = new(3, "testInvestmentAccount1");
            investmentAccount1.Add(new InvestmentEntry(1, startDate, 10, 10, "testStock1", InvestmentType.Stock));
            investmentAccount1.Add(new InvestmentEntry(2, endDate, 10, 10, "testStock2", InvestmentType.Stock));

            _investmentAccountAccounts = [investmentAccount1];
            _financalAccountRepositoryMock.Setup(x => x.GetAccounts<InvestmentAccount>(startDate, endDate))
                .Returns(_investmentAccountAccounts);

            totalAssetsValue = 90;

            _stockRepository.Setup(x => x.GetStockPrice("testStock1", It.IsAny<DateTime>()))
                            .ReturnsAsync(new StockPrice() { Currency = "PLN", Ticker = "AnyTicker", PricePerUnit = 2 });
            _stockRepository.Setup(x => x.GetStockPrice("testStock2", It.IsAny<DateTime>()))
                           .ReturnsAsync(new StockPrice() { Currency = "PLN", Ticker = "AnyTicker", PricePerUnit = 4 });

            _moneyFlowService = new MoneyFlowService(_financalAccountRepositoryMock.Object, _stockRepository.Object);
        }

        [Fact]
        public async Task GetAssetsPerAcount()
        {
            // Arrange

            // Act
            var result = await _moneyFlowService.GetEndAssetsPerAcount(startDate, endDate);

            // Assert
            Assert.Equal(_bankAccounts.Count + _investmentAccountAccounts.Count, result.Count);
            Assert.Equal(totalAssetsValue, result.Sum(x => x.Value));
        }

        [Fact]
        public async Task GetEndAssetsPerType()
        {
            // Arrange

            // Act
            var result = await _moneyFlowService.GetEndAssetsPerType(startDate, endDate);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(totalAssetsValue, result.Sum(x => x.Value));
        }

        [Fact]
        public async Task GetAssetsPerTypeTimeseries()
        {
            // Arrange

            // Act
            var result = await _moneyFlowService.GetAssetsTimeSeries(startDate, endDate);

            // Assert
            Assert.NotEmpty(result);
            Assert.Equal(totalAssetsValue, result.First(x => x.DateTime == endDate).Value);
        }

        [Theory]
        [InlineData(InvestmentType.Bond, 0)]
        [InlineData(InvestmentType.Stock, 60)]
        [InlineData(InvestmentType.Cash, 30)]
        [InlineData(InvestmentType.Property, 0)]
        public async Task GetAssetsPerTypeTimeseries_TypeAsParameter(InvestmentType investmentType, decimal finalValue)
        {
            // Arrange
            // Act
            var result = await _moneyFlowService.GetAssetsTimeSeries(startDate, endDate, investmentType);

            // Assert
            if (finalValue == 0)
            {
                Assert.Empty(result);
            }
            else
            {
                Assert.Equal(result.First().Value, finalValue);
                Assert.Equal(result.First(x => x.DateTime == endDate).Value, finalValue);
            }
        }

    }
}