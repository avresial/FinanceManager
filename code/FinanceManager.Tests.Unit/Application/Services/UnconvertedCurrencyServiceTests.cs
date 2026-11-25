using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class UnconvertedCurrencyServiceTests
{
    private const int _plnId = 0;
    private const int _eurId = 2;
    private const int _usdId = 3;
    private static readonly DateTime _day = DateTime.UtcNow.AddDays(-5);
    private static readonly Currency _pln = new(_plnId, "PLN", "zł");
    private static readonly Currency _eur = new(_eurId, "EUR", "€");
    private static readonly Currency _usd = new(_usdId, "USD", "$");

    private readonly InMemoryOwnerLedger _cash = new();
    private readonly InMemoryOwnerLedger _bonds = new();
    private readonly Mock<IBondDetailsRepository> _bondDetails = new();
    private readonly UnconvertedCurrencyService _service;

    public UnconvertedCurrencyServiceTests()
    {
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(x => x.GetCurrency(_plnId, It.IsAny<CancellationToken>())).ReturnsAsync(_pln);
        currencies.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>())).Returns(() => new[] { _pln, _eur, _usd }.ToAsyncEnumerable());
        _bondDetails.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) => ids.Select(id => Bond(id, id == 1 ? _usd : _pln)).ToList());
        _service = new(_cash, _bonds, new BondDashboardContext(_bondDetails.Object), currencies.Object);
    }

    [Fact]
    public async Task GetCodes_IsEmpty_WhenEverythingIsInTheDisplayCurrency()
    {
        _cash.Add(_plnId, _day, 100m);
        _bonds.Add(2, _day, 3m);

        Assert.Empty(await _service.GetCodes(1, _plnId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCodes_NamesForeignCashAndBondCurrencies_ExcludingThoseNettedToZero()
    {
        _cash.Add(_plnId, _day, 100m).Add(_eurId, _day, -30m).Add(_usdId, _day, 5m).Add(_usdId, _day.AddDays(1), -5m);
        _bonds.Add(1, _day, 2m);

        Assert.Equal(["EUR", "USD"], await _service.GetCodes(-4, _plnId, TestContext.Current.CancellationToken));
    }

    private static BondDetails Bond(int id, Currency currency) =>
        new("Bond", "Issuer", DateOnly.FromDateTime(_day), DateOnly.FromDateTime(_day.AddYears(5)),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = _day.AddYears(5).ToString("yyyy-MM-dd"), Rate = 0 }],
            currency, BondType.InflationBond, 100m)
        { Id = id };
}