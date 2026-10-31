using FinanceManager.Application.FinancialAccounts.Investments.Dividends;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class DividendPaymentServiceTests
{
    private readonly Mock<IDividendPaymentRepository> _payments = new();
    private readonly Mock<IAccountRepository<InvestmentAccount>> _accounts = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private DividendPaymentService Service => new(_payments.Object, _accounts.Object, _currencies.Object);
    public DividendPaymentServiceTests()
    {
        _accounts.Setup(x => x.Get(4)).ReturnsAsync(new InvestmentAccount(1, 4, "Broker"));
        _currencies.Setup(x => x.GetByCode("USD", It.IsAny<CancellationToken>())).ReturnsAsync(new Currency(1, "USD", "$"));
        _payments.Setup(x => x.HasHolding(1, 4, 8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payments.Setup(x => x.Add(It.IsAny<DividendPayment>(), It.IsAny<CancellationToken>())).ReturnsAsync((DividendPayment p, CancellationToken _) => p);
    }
    private static DividendPaymentRequest Request() => new() { AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 12.34m, Currency = " usd ", Notes = " received " };

    [Fact]
    public async Task Save_NormalizesCashRecordWithoutTradeWrites()
    {
        await Service.Save(1, 4, null, Request(), TestContext.Current.CancellationToken);
        _payments.Verify(x => x.Add(It.Is<DividendPayment>(p => p.AccountId == 4 && p.UserId == 1 && p.AssetListingId == 8 && p.Amount == 12.34m && p.Currency == "USD" && p.Notes == "received" && p.CreatedAt != default), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task OtherUsersAccount_IsRejectedBeforeReadingPayments()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.GetHistory(2, 4, 1, 20, TestContext.Current.CancellationToken));
        _payments.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task ForgedRecordId_IsRejectedOnUpdateAndDelete()
    {
        _payments.Setup(x => x.Get(9, It.IsAny<CancellationToken>())).ReturnsAsync(new DividendPayment { Id = 9, UserId = 2, AccountId = 5 });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.Save(1, 4, 9, Request(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.Delete(1, 4, 9, TestContext.Current.CancellationToken));
        _payments.Verify(x => x.Save(It.IsAny<CancellationToken>()), Times.Never);
        _payments.Verify(x => x.Delete(It.IsAny<DividendPayment>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000000000)]
    public async Task InvalidAmount_IsRejected(decimal amount)
    {
        var request = Request(); request.Amount = amount;
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, 4, null, request, TestContext.Current.CancellationToken));
        _payments.Verify(x => x.Add(It.IsAny<DividendPayment>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task FutureDateUnknownCurrencyAndUnownedHolding_AreRejected()
    {
        var request = Request(); request.PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, 4, null, request, TestContext.Current.CancellationToken));
        request = Request(); request.Currency = "XYZ";
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, 4, null, request, TestContext.Current.CancellationToken));
        request = Request(); request.AssetListingId = 99;
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, 4, null, request, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task ExistingRecordRemainsEditableAfterTradesRemoved()
    {
        var record = new DividendPayment { Id = 7, AccountId = 4, UserId = 1, AssetListingId = 8, Amount = 1 };
        _payments.Setup(x => x.Get(7, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _payments.Setup(x => x.HasHolding(1, 4, 8, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Service.Save(1, 4, 7, Request(), TestContext.Current.CancellationToken);
        Assert.Equal(12.34m, record.Amount);
        _payments.Verify(x => x.Save(It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 51)]
    [InlineData(2147483647, 20)]
    public async Task InvalidPaging_IsRejected(int page, int size) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetHistory(1, 4, page, size, TestContext.Current.CancellationToken));
}