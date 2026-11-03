using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class NetWorthServiceTests
{
    private readonly NetWorthService _netWorthService;
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepositoryMock = new();
    private readonly Mock<IInvestmentValuationService> _investmentValuationServiceMock = new();

    public NetWorthServiceTests()
    {
        _bondDetailsRepositoryMock.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<BondDetails>());

        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<BondAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());
        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<InvestmentAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<InvestmentAccount>());

        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueSeriesAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>());
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueSeriesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>());

        _netWorthService = new NetWorthService(
            _financialAccountRepositoryMock.Object,
            new BondDashboardContext(_bondDetailsRepositoryMock.Object),
            _investmentValuationServiceMock.Object);
    }

    [Fact]
    public async Task GetNetWorth_NoAccounts_ReturnsZero()
    {
        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, DateTime.UtcNow);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetNetWorth_CurrencyAccount_IncludesBalance()
    {
        var date = DateTime.UtcNow;
        var account = new CurrencyAccount(1, 1, "Cash", AccountLabel.Other);
        account.Add(new CurrencyAccountEntry(1, 1, date.AddDays(-1), 0, 100m) { Labels = [] });

        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<CurrencyAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, date);

        Assert.Equal(100m, result);
    }

    [Fact]
    public async Task GetNetWorth_InvestmentAccount_IncludesValuation()
    {
        var date = DateTime.UtcNow;
        var account = new InvestmentAccount(1, 5, "Investments");

        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<InvestmentAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueAsync(5, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(250m);

        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, date);

        Assert.Equal(250m, result);
    }

    [Fact]
    public async Task GetNetWorthSeries_InvestmentAccount_UsesDailyValuationSeries()
    {
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-2);
        var account = new InvestmentAccount(1, 5, "Investments");

        _financialAccountRepositoryMock.Setup(r => r.GetAccounts<InvestmentAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());

        var series = new Dictionary<DateTime, decimal>
        {
            [start] = 10m,
            [start.AddDays(1)] = 20m,
            [end] = 30m
        };
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueSeriesAsync(It.Is<IReadOnlyCollection<int>>(a => a.Contains(5)), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>> { [5] = series });

        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, start, end);

        Assert.Equal(10m, result[start]);
        Assert.Equal(30m, result[end]);
    }

    [Fact]
    public async Task GetNetWorth_OverRange_KeysEachPointByCalendarDay()
    {
        // The dashboard passes "now" as the end, so the walk back carries the time of day.
        // Keying by date keeps the series on the same day grid as the other dashboard series.
        var start = new DateTime(2024, 3, 3, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 3, 6, 20, 23, 14, DateTimeKind.Utc);

        var result = await _netWorthService.GetNetWorth(1, DefaultCurrency.PLN, start, end);

        Assert.Equal([start, start.AddDays(1), start.AddDays(2), start.AddDays(3)], result.Keys.Order());
    }

    [Fact]
    public async Task GetNetWorth_OverRange_FetchesAllInvestmentAccountsInOneBatchedCall()
    {
        // The per-account fan-out is replaced by a single batched valuation call that carries every
        // investment account id. This keeps the shared scoped AppDbContext single-threaded (one query
        // for all accounts) instead of one round-trip per account.
        const int userId = 1;
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        List<InvestmentAccount> investmentAccounts =
        [
            new(userId, 1, "Investments A"),
            new(userId, 2, "Investments B"),
            new(userId, 3, "Investments C")
        ];

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<InvestmentAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(investmentAccounts.ToAsyncEnumerable());
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<CurrencyAccount>());
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(AsyncEnumerable.Empty<BondAccount>());

        List<int>? capturedIds = null;
        _investmentValuationServiceMock
            .Setup(x => x.GetAccountValueSeriesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<int> ids, Currency _, DateTime _, DateTime _, CancellationToken _) => capturedIds = ids.ToList())
            .ReturnsAsync(new Dictionary<int, IReadOnlyDictionary<DateTime, decimal>>());

        await _netWorthService.GetNetWorth(userId, DefaultCurrency.PLN, start, end);

        // Exactly one batched call, carrying all three account ids.
        _investmentValuationServiceMock.Verify(
            x => x.GetAccountValueSeriesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _investmentValuationServiceMock.Verify(
            x => x.GetAccountValueSeriesAsync(It.IsAny<int>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.NotNull(capturedIds);
        Assert.Equal(new[] { 1, 2, 3 }, capturedIds!.OrderBy(x => x));
    }

    [Fact]
    public async Task GetNetWorth_OverRange_BondAccounts_SumsEveryStoredBondPerDay()
    {
        // Rate-0 definitions keep each price exact: entry value x unit value. The accruing bond adds
        // fractional prices so the totals also prove the final two-decimal rounding.
        const int userId = 1;
        const int sharedBondId = 1;
        const int boundaryOnlyBondId = 2;
        const int accruingBondId = 3;
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var middle = start.AddDays(1);
        var end = start.AddDays(2);

        // Account 10 holds the shared bond both in range and at the older boundary, plus a bond that
        // only exists at the boundary. Account 20 holds the shared bond and changes it within the
        // window, plus an accruing bond held at the boundary. Account 30 is empty.
        BondAccount boundaryAndInRangeAccount = new(userId, 10, "Bonds A",
            [new BondAccountEntry(10, 1, middle, 5, 3, sharedBondId)],
            nextOlderEntries: new Dictionary<int, BondAccountEntry>
            {
                [sharedBondId] = new(10, 2, new DateTime(2023, 12, 20, 0, 0, 0, DateTimeKind.Utc), 2, 2, sharedBondId),
                [boundaryOnlyBondId] = new(10, 3, new DateTime(2023, 12, 1, 0, 0, 0, DateTimeKind.Utc), 3, 3, boundaryOnlyBondId)
            });
        BondAccount changingAccount = new(userId, 20, "Bonds B",
        [
            new BondAccountEntry(20, 5, end, 4, 3, sharedBondId),
            new BondAccountEntry(20, 4, start, 1, 1, sharedBondId)
        ],
            nextOlderEntries: new Dictionary<int, BondAccountEntry>
            {
                [accruingBondId] = new(20, 6, new DateTime(2023, 12, 31, 0, 0, 0, DateTimeKind.Utc), 100, 100, accruingBondId)
            });
        BondAccount emptyAccount = new(userId, 30, "Bonds C");

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BondAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { boundaryAndInRangeAccount, changingAccount, emptyAccount }.ToAsyncEnumerable());
        _bondDetailsRepositoryMock.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateBond(sharedBondId, 100m), CreateBond(boundaryOnlyBondId, 10m), CreateBond(accruingBondId, 1m, 0.04m)]);

        var result = await _netWorthService.GetNetWorth(userId, DefaultCurrency.PLN, start, end);

        // The accruing bond gains round(100 * 0.04 / 365, 5) = 0.01096 units per day after 2023-12-31.
        // start:  A shared boundary 2*100 + A boundary-only 3*10 + B 1*100 + 100.01096 = 430.01096 -> 430.01
        // middle: A shared in range 5*100 + 30 + B 1*100 + 100.02192 = 730.02192 -> 730.02
        // end:    500 + 30 + B changed 4*100 + 100.03288 = 1030.03288 -> 1030.03
        Assert.Equal([end, middle, start], result.Keys);
        Assert.Equal(430.01m, result[start]);
        Assert.Equal(730.02m, result[middle]);
        Assert.Equal(1030.03m, result[end]);
    }

    [Fact]
    public async Task GetNetWorth_OverRange_LongHeldBond_PreparesHistoryOnce()
    {
        var end = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = end.AddDays(-30);
        var entry = new BondAccountEntry(10, 1, start.AddDays(-3650), 1m, 1m, 1);
        var details = CreateBond(1, 100m, 0.05m);
        var account = new BondAccount(1, 10, "Long-held", nextOlderEntries: new() { [1] = entry });
        SetBonds([account], [details]);

        // Warm JIT outside measurement; each operation has a new request context.
        await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end);
        var beforeHistory = GC.GetAllocatedBytesForCurrentThread();
        var history = entry.GetPrice(DateOnly.FromDateTime(end), details);
        var historyBytes = GC.GetAllocatedBytesForCurrentThread() - beforeHistory;
        var service = NewService();
        var beforeRange = GC.GetAllocatedBytesForCurrentThread();
        var pending = service.GetNetWorth(1, DefaultCurrency.PLN, start, end);
        // All mock I/O completes synchronously, so the per-thread allocation comparison is valid.
        Assert.True(pending.IsCompletedSuccessfully);
        var result = await pending;
        var rangeBytes = GC.GetAllocatedBytesForCurrentThread() - beforeRange;

        Assert.Equal(31, result.Count);
        Assert.All(result, point => Assert.Equal(Math.Round(history[DateOnly.FromDateTime(point.Key)], 2), point.Value));
        Assert.True(rangeBytes < historyBytes * 4 + 100_000,
            $"Range allocated {rangeBytes} bytes versus {historyBytes} for one bounded history.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task GetNetWorth_OverRange_MatchesPointPricing_WithChangingDuplicateAndBoundaryEntries(int hour)
    {
        var start = new DateTime(2023, 12, 28, hour, 13, 0, DateTimeKind.Utc);
        var end = start.AddDays(12);
        var details = CreateBond(1, 1.23456m);
        details.CalculationMethods =
        [
            new() { BondDetails = details, DateOperator = DateOperator.UntilDate, DateValue = "2024-01-02", Rate = 0.037m },
            // An inactive method leaves a genuine gap after the last active method expires.
            new() { BondDetails = null, DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = 1m },
            new() { BondDetails = details, DateOperator = DateOperator.UntilDate, DateValue = "2024-01-05", Rate = 0.061m }
        ];
        var otherDetails = CreateBond(2, 100m, 0.02m);
        BondAccount[] accounts =
        [
            new(1, 10, "A",
            [
                new(10, 7, start.AddDays(4).Date.AddHours(18), 3.25m, 0m, 1),
                new(10, 8, start.AddDays(4).Date.AddHours(18), 99m, 0m, 1),
                new(10, 9, start.AddDays(4).Date.AddHours(8), 5m, 0m, 1),
                new(10, 10, start.AddDays(8), 0m, -3.25m, 1),
                new(10, 11, start.AddDays(9), 2.75m, 2.75m, 1),
                new(10, 12, start.AddDays(1), 0m, 0m, 2)
            ], nextOlderEntries: new() { [1] = new(10, 1, start.Date.AddDays(-365), 2.12m, 0m, 1) }),
            // Same entry id with different inputs/account must not share history.
            new(1, 20, "A", nextOlderEntries: new() { [1] = new(20, 1, start.Date.AddDays(-730), 7.13m, 0m, 1) }),
            new(1, 30, "Empty")
        ];
        SetBonds(accounts, [details, otherDetails]);
        var expected = new Dictionary<DateTime, decimal>();
        for (var date = end; date >= start; date = date.AddDays(-1))
        {
            decimal total = 0;
            foreach (var account in accounts)
                foreach (var id in account.GetStoredBondsIds())
                {
                    var entry = account.GetThisOrNextOlder(date, id);
                    if (entry is not null)
                        total += entry.GetPriceAt(DateOnly.FromDateTime(date), id == 1 ? details : otherDetails);
                }
            expected.Add(date.Date, Math.Round(total, 2));
        }

        Assert.Equal(expected, await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end));
        Assert.Empty(await NewService().GetNetWorth(1, DefaultCurrency.PLN, end, start));
    }

    [Fact]
    public async Task GetNetWorth_NewOperationAndRequest_UsesCurrentEntryAndDefinitionInputs()
    {
        var start = new DateTime(2024, 1, 1);
        var end = start.AddDays(3);
        var entry = new BondAccountEntry(10, 1, start.AddDays(-365), 1m, 1m, 1);
        var account = new BondAccount(1, 10, "Bonds", nextOlderEntries: new() { [1] = entry });
        var details = CreateBond(1, 100m, 0.05m);
        SetBonds([account], [details]);
        var service = NewService();
        var original = await service.GetNetWorth(1, DefaultCurrency.PLN, start, end);
        entry.Value = 2m;
        entry.PostingDate = entry.PostingDate.AddDays(1);
        var changed = await service.GetNetWorth(1, DefaultCurrency.PLN, start, end);
        Assert.NotEqual(original[end], changed[end]);
        Assert.Equal(Math.Round(entry.GetPriceAt(DateOnly.FromDateTime(end), details), 2), changed[end]);

        details = CreateBond(2, 125m, 0.07m);
        entry.BondDetailsId = 2;
        SetBonds([new BondAccount(1, 10, "Bonds", nextOlderEntries: new() { [2] = entry })], [details]);
        var fresh = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, end.AddDays(2));
        Assert.Equal(Math.Round(entry.GetPriceAt(DateOnly.FromDateTime(end), details), 2), fresh[end]);
    }

    [Fact]
    public async Task GetNetWorth_OverRange_MissingDefinitionOrMethods_PreservesErrors()
    {
        var date = new DateTime(2024, 1, 1);
        var entry = new BondAccountEntry(10, 1, date, 0m, 0m, 1);
        var account = new BondAccount(1, 10, "Bonds", [entry]);
        SetBonds([account], []);
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().GetNetWorth(1, DefaultCurrency.PLN, date, date));
        Assert.Equal("Bond valuation requires details for bond id 1.", missing.Message);
        var details = CreateBond(1, 100m);
        details.CalculationMethods.Clear();
        SetBonds([account], [details]);
        var methods = await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().GetNetWorth(1, DefaultCurrency.PLN, date, date));
        Assert.Equal("CalculationMethods can't be empty", methods.Message);
    }

    [Fact]
    public async Task GetNetWorth_OverRange_BoundsHistoryByLatestSelectedDay_AndSkipsPrePostingCalculation()
    {
        var start = new DateTime(2024, 1, 1);
        var details = CreateBond(1, 100m);
        details.CalculationMethods =
        [
            new() { BondDetails = details, DateValue = "2024-01-02", Rate = 0.05m },
            // This method is never inspected while the first method covers the selected day.
            new() { BondDetails = details, DateValue = "invalid", Rate = 1m }
        ];
        var old = new BondAccountEntry(10, 1, start.AddDays(-365), 1m, 1m, 1);
        var futureDetails = CreateBond(2, 100m);
        futureDetails.CalculationMethods.Clear();
        var future = new BondAccountEntry(20, 3, start.AddDays(30), 1m, 1m, 2);
        var replacement = new BondAccountEntry(10, 2, start.AddDays(2), 2m, 1m, 1);
        SetBonds([new BondAccount(1, 10, "Old", [replacement, old])], [details]);
        var shortRange = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, start.AddDays(2));
        Assert.Equal(Math.Round(old.GetPriceAt(DateOnly.FromDateTime(start), details), 2), shortRange[start]);

        // A malformed boundary snapshot before posting still follows GetPriceAt's early-zero rule.
        SetBonds([new BondAccount(1, 20, "Future", nextOlderEntries: new() { [2] = future })], [futureDetails]);
        var prePosting = await NewService().GetNetWorth(1, DefaultCurrency.PLN, start, start.AddDays(1));
        Assert.All(prePosting.Values, value => Assert.Equal(0m, value));
    }

    private void SetBonds(BondAccount[] accounts, BondDetails[] details)
    {
        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BondAccount>(1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(accounts.ToAsyncEnumerable());
        _bondDetailsRepositoryMock.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
    }

    private NetWorthService NewService() => new(
        _financialAccountRepositoryMock.Object,
        new BondDashboardContext(_bondDetailsRepositoryMock.Object),
        _investmentValuationServiceMock.Object);

    private static BondDetails CreateBond(int id, decimal unitValue, decimal rate = 0m) =>
        new(
            $"Bond {id}",
            "Issuer",
            new DateOnly(2023, 1, 1),
            new DateOnly(2030, 1, 1),
            [new BondCalculationMethod { DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = rate }],
            DefaultCurrency.PLN,
            BondType.InflationBond,
            unitValue)
        {
            Id = id
        };
}