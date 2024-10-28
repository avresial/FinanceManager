using FinanceManager.Application.Services;
using FinanceManager.Core.Entities;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;
using Moq;

namespace FinanceManager.UnitTests
{
    public class MoneyFlowServiceTests
    {
        private DateTime startDate = new DateTime(2020, 1, 1);
        private DateTime endDate = new DateTime(2020, 1, 31);

        private MoneyFlowService _moneyFlowService;
        private Mock<IFinancalAccountRepository> _financalAccountRepositoryMock = new Mock<IFinancalAccountRepository>();
        private Mock<IStockRepository> _stockRepository = new Mock<IStockRepository>();
        private List<BankAccount> _bankAccounts;
        private List<InvestmentAccount> _investmentAccountAccounts;

        public MoneyFlowServiceTests()
        {
            BankAccount bankAccount1 = new BankAccount("testBank1", AccountType.Cash);
            bankAccount1.Add(new BankAccountEntry(startDate, 10, 10));
            bankAccount1.Add(new BankAccountEntry(startDate.AddDays(1), 20, 10));

            BankAccount bankAccount2 = new BankAccount("testBank2", AccountType.Cash);
            bankAccount2.Add(new BankAccountEntry(endDate, 10, 10));
            _bankAccounts = new List<BankAccount>() { bankAccount1, bankAccount2 };
            _financalAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(startDate, endDate))
                                            .Returns(_bankAccounts);

            InvestmentAccount investmentAccount1 = new InvestmentAccount("testInvestmentAccount1");
            investmentAccount1.Add(new InvestmentEntry(startDate, 10, 10, "testStock1", InvestmentType.Stock));
            investmentAccount1.Add(new InvestmentEntry(endDate, 10, 10, "testStock2", InvestmentType.Stock));
            _investmentAccountAccounts = new List<InvestmentAccount>() { investmentAccount1 };
            _financalAccountRepositoryMock.Setup(x => x.GetAccounts<InvestmentAccount>(startDate, endDate))
                .Returns(_investmentAccountAccounts);

            _stockRepository.Setup(x => x.GetStockPrice(It.IsAny<string>(), It.IsAny<DateTime>()))
                            .ReturnsAsync(new StockPrice() { Currency = "PLN", Ticker = "AnyTicker", PricePerUnit = 2 });

            _moneyFlowService = new MoneyFlowService(_financalAccountRepositoryMock.Object, _stockRepository.Object, new SettingsService());
        }

        [Fact]
        public async Task GetAssetsPerAcount()
        {
            // Arrange

            // Act
            var result = await _moneyFlowService.GetEndAssetsPerAcount(startDate, endDate);

            // Assert
            Assert.Equal(_bankAccounts.Count + _investmentAccountAccounts.Count, result.Count);
            Assert.Equal(70, result.Sum(x => x.Value));
        }

        [Fact]
        public async Task GetEndAssetsPerType()
        {
            // Arrange

            // Act
            var result = await _moneyFlowService.GetEndAssetsPerType(startDate, endDate);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(70, result.Sum(x => x.Value));
        }
    }
}