using FinanceManager.Application.Portfolio.Dividends;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Portfolio.Dividends.Dtos;
using FinanceManager.Domain.Portfolio.Dividends.Entities;
using FinanceManager.Domain.Portfolio.Dividends.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class DividendPaymentServiceTests
{
    private readonly Mock<IDividendPaymentRepository> _payments = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private DividendPaymentService Service => new(_payments.Object, _currencies.Object);
    public DividendPaymentServiceTests()
    {
        _currencies.Setup(x => x.GetByCode("USD", It.IsAny<CancellationToken>())).ReturnsAsync(new Currency(1, "USD", "$"));
        _payments.Setup(x => x.HasHolding(1, 8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payments.Setup(x => x.Add(It.IsAny<DividendPayment>(), It.IsAny<CancellationToken>())).ReturnsAsync((DividendPayment p, CancellationToken _) => p);
    }
    private static DividendPaymentRequest Request() => new() { AssetListingId = 8, PaymentDate = new(2020, 1, 1), Amount = 12.34m, Currency = " usd ", Notes = " received " };

    [Fact]
    public async Task Save_NormalizesCashRecordWithoutTradeWrites()
    {
        await Service.Save(1, null, Request(), TestContext.Current.CancellationToken);
        _payments.Verify(x => x.Add(It.Is<DividendPayment>(p => p.UserId == 1 && p.AssetListingId == 8 && p.Amount == 12.34m && p.Currency == "USD" && p.Notes == "received" && p.CreatedAt != default), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgedRecordId_IsRejectedOnUpdateAndDelete()
    {
        _payments.Setup(x => x.Get(9, It.IsAny<CancellationToken>())).ReturnsAsync(new DividendPayment { Id = 9, UserId = 2, AccountId = 5 });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.Save(1, 9, Request(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.Delete(1, 9, TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, null, request, TestContext.Current.CancellationToken));
        _payments.Verify(x => x.Add(It.IsAny<DividendPayment>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task FutureDateUnknownCurrencyAndUnownedHolding_AreRejected()
    {
        var request = Request(); request.PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, null, request, TestContext.Current.CancellationToken));
        request = Request(); request.Currency = "XYZ";
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, null, request, TestContext.Current.CancellationToken));
        request = Request(); request.AssetListingId = 99;
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, null, request, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task ExistingRecordRemainsEditableAfterTradesRemoved()
    {
        var record = new DividendPayment { Id = 7, AccountId = 4, UserId = 1, AssetListingId = 8, Amount = 1 };
        _payments.Setup(x => x.Get(7, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _payments.Setup(x => x.HasHolding(1, 8, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Service.Save(1, 7, Request(), TestContext.Current.CancellationToken);
        Assert.Equal(12.34m, record.Amount);
        _payments.Verify(x => x.Save(It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 51)]
    [InlineData(2147483647, 20)]
    public async Task InvalidPaging_IsRejected(int page, int size) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetHistory(1, page, size, TestContext.Current.CancellationToken));

    [Fact]
    public async Task OwnerWide_NewPaymentIsUnlinked_AndNoAccountIsLookedUp()
    {
        _payments.Setup(x => x.HasHolding(1, 8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Service.Save(1, null, Request(), TestContext.Current.CancellationToken);
        _payments.Verify(x => x.Add(It.Is<DividendPayment>(p => p.AccountId == null && p.UserId == 1 && p.Currency == "USD"), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task OwnerWide_ForeignRecord_IsReportedAsMissing_AndLinkedRecordOfTheOwnerIsEditable()
    {
        _payments.Setup(x => x.Get(9, It.IsAny<CancellationToken>())).ReturnsAsync(new DividendPayment { Id = 9, UserId = 2, AccountId = null });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.Save(1, 9, Request(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.Delete(1, 9, TestContext.Current.CancellationToken));

        var linked = new DividendPayment { Id = 10, UserId = 1, AccountId = 4, AssetListingId = 8 };
        _payments.Setup(x => x.Get(10, It.IsAny<CancellationToken>())).ReturnsAsync(linked);
        await Service.Save(1, 10, Request(), TestContext.Current.CancellationToken);
        Assert.Equal(4, linked.AccountId);
        Assert.Equal(12.34m, linked.Amount);
    }
    [Fact]
    public async Task OwnerWide_SharesTheScalarValidationWithTheAccountPath()
    {
        var request = Request(); request.Amount = 0;
        await Assert.ThrowsAsync<ArgumentException>(() => Service.Save(1, null, request, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetHistory(1, 0, 20, TestContext.Current.CancellationToken));
        _payments.Verify(x => x.Add(It.IsAny<DividendPayment>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}