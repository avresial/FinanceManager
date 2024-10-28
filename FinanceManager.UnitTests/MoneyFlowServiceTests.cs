using FinanceManager.Application.Services;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;
using Moq;

namespace FinanceManager.UnitTests
{
    public class MoneyFlowServiceTests
    {
        private DateTime start = new DateTime(2020, 1, 1);
        private DateTime end = new DateTime(2020, 1, 31);
        private MoneyFlowService _moneyFlowService;
        private Mock<IFinancalAccountRepository> _financalAccountRepositoryMock = new Mock<IFinancalAccountRepository>();
        public MoneyFlowServiceTests()
        {
            BankAccount bankAccount = new BankAccount("testBank1", AccountType.Cash);
            bankAccount.Add(new BankAccountEntry(start, 10, 10));
            bankAccount.Add(new BankAccountEntry(start.AddDays(1), 20, 10));

            _financalAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(start, end))
                .Returns(new List<BankAccount>()
                {
                   bankAccount
                });

            _moneyFlowService = new MoneyFlowService(_financalAccountRepositoryMock.Object, null, null);
        }

        [Fact]
        public void GetAssetsPerAcount()
        {
            // Arrange

            // Act
            var result = _moneyFlowService.GetAssetsPerAcount(start, end);

            // Assert
            Assert.Equal(2, result.Count);
        }
    }
}