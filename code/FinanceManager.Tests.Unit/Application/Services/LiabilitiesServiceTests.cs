using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class LiabilitiesServiceTests
{
    private const int _plnId = 0;
    private const int _eurId = 2;
    private static readonly DateTime _day = new(2024, 3, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly LiabilitiesService _service;

    public LiabilitiesServiceTests()
    {
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(() => new[] { new Currency(_plnId, "PLN", "zł"), new Currency(_eurId, "EUR", "€") }.ToAsyncEnumerable());
        _service = new(_ledger, currencies.Object);
    }

    [Fact]
    public async Task IsAnyAccountWithLiabilities_IsTrue_WhenACurrencyNetsNegative()
    {
        _ledger.Add(_plnId, DateTime.UtcNow.AddDays(-1), -100m);

        Assert.True(await _service.IsAnyAccountWithLiabilities(1));
    }

    [Fact]
    public async Task IsAnyAccountWithLiabilities_IsFalse_WhenPositiveCashOffsetsTheLoanInTheSameCurrency()
    {
        _ledger.Add(_plnId, DateTime.UtcNow.AddDays(-2), 1000m).Add(_plnId, DateTime.UtcNow.AddDays(-1), -400m);

        Assert.False(await _service.IsAnyAccountWithLiabilities(1));
    }

    [Fact]
    public async Task IsAnyAccountWithLiabilities_AcceptsNegativeGuestIds()
    {
        _ledger.Add(_plnId, DateTime.UtcNow.AddDays(-1), -100m);

        Assert.True(await _service.IsAnyAccountWithLiabilities(-7));
    }

    [Fact]
    public async Task GetEndLiabilitiesPerAccount_NamesEachNegativeCurrencyByItsCode()
    {
        _ledger.Add(_plnId, _day, -200m).Add(_eurId, _day, -30m).Add(_eurId, _day.AddHours(1), 10m);

        var list = await _service.GetEndLiabilitiesPerAccount(1, _day, _day.AddDays(1)).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([("PLN", -200m), ("EUR", -20m)], list.Select(x => (x.Name, x.Value)));
        Assert.Equal(["PLN", "EUR"], list.Select(x => x.CurrencyCode));
    }

    [Fact]
    public async Task GetEndLiabilitiesPerAccount_SkipsCurrenciesThatNetPositive()
    {
        _ledger.Add(_plnId, _day, 500m).Add(_plnId, _day.AddHours(1), -200m).Add(_eurId, _day, -50m);

        var list = await _service.GetEndLiabilitiesPerAccount(1, _day, _day.AddDays(1)).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal("EUR", Assert.Single(list).Name);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerAccount_FallsBackToTheCurrencyIdForAnUnknownCurrency()
    {
        _ledger.Add(77, _day, -5m);

        var list = await _service.GetEndLiabilitiesPerAccount(1, _day, _day.AddDays(1)).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Currency 77", Assert.Single(list).Name);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerType_SumsNegativeCurrenciesUnderCash()
    {
        _ledger.Add(_plnId, _day, -100m).Add(_eurId, _day, -50m).Add(3, _day, 900m);

        var list = await _service.GetEndLiabilitiesPerType(1, _day, _day.AddDays(1)).ToListAsync(TestContext.Current.CancellationToken);

        var result = Assert.Single(list);
        Assert.Equal("Cash", result.Name);
        Assert.Equal(-150m, result.Value);
        // A mix of currencies has no single code to claim.
        Assert.Null(result.CurrencyCode);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerType_NamesTheCurrency_WhenEveryLiabilityIsInOne()
    {
        _ledger.Add(_eurId, _day, -30m).Add(_eurId, _day.AddDays(1), -10m).Add(_plnId, _day, 900m);

        var result = Assert.Single(await _service.GetEndLiabilitiesPerType(1, _day, _day.AddDays(2)).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(-40m, result.Value);
        Assert.Equal("EUR", result.CurrencyCode);
    }

    [Fact]
    public async Task GetEndLiabilitiesPerType_IsEmpty_WhenNothingIsNegative()
    {
        _ledger.Add(_plnId, _day, 100m);

        Assert.Empty(await _service.GetEndLiabilitiesPerType(1, _day, _day.AddDays(1)).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLiabilitiesTimeSeries_SumsNegativeCurrencyBalancesPerDay()
    {
        _ledger.Add(_plnId, _day, -100m).Add(_eurId, _day.AddDays(1), -30m).Add(_plnId, _day.AddDays(1), -20m);

        var list = await _service.GetLiabilitiesTimeSeries(1, _day, _day.AddDays(2)).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([-150m, -150m, -100m], list.Select(x => x.Value));
        Assert.Equal([_day.AddDays(2), _day.AddDays(1), _day], list.Select(x => x.DateTime));
    }

    [Fact]
    public async Task GetLiabilitiesTimeSeries_ShowsTheBalanceTurningNegativeMidRange()
    {
        _ledger.Add(_plnId, _day.AddHours(-1), 100m).Add(_plnId, _day.AddDays(1).AddHours(8), -300m);

        var list = await _service.GetLiabilitiesTimeSeries(1, _day, _day.AddDays(2)).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([-200m, -200m, 0m], list.Select(x => x.Value));
    }

    [Fact]
    public async Task GetLiabilitiesTimeSeries_IsEmpty_WhenEveryBalanceStaysPositive()
    {
        _ledger.Add(_plnId, _day, 100m);

        Assert.Empty(await _service.GetLiabilitiesTimeSeries(1, _day, _day.AddDays(2)).ToListAsync(TestContext.Current.CancellationToken));
    }
}