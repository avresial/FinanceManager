using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// The owner readers behind money-flow reports, the recurring-payment detector, the guest seeders and the plan verifier, on a
/// relational provider: label totals and labelled entries over linked and unlinked rows, window bounds, owner isolation and
/// guest ids. Whole amounts keep SQLite's decimal-as-TEXT storage out of the assertions.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OwnerMoneyFlowQueryTests : IDisposable
{
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -3;
    private static readonly DateTime _jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly CashTransactionRepository _cash;
    private readonly BondTransactionRepository _bonds;
    private readonly InvestmentTransactionRepository _investments;
    private readonly FinancialLabel _salary = new() { Name = "Salary" };
    private readonly FinancialLabel _rent = new() { Name = "Rent" };

    public OwnerMoneyFlowQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.FinancialLabels.AddRange(_salary, _rent);
        _context.SaveChanges();
        _cash = new CashTransactionRepository(_context);
        _bonds = new BondTransactionRepository(_context);
        _investments = new InvestmentTransactionRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task AddCash(int owner, int? currency, DateTime date, decimal change, int? account = null, string description = "", string? contractor = null, params FinancialLabel[] labels)
    {
        _context.CurrencyEntries.Add(new CurrencyAccountEntry(account, 0, date, 999, change) { UserId = owner, CurrencyId = currency, Description = description, ContractorDetails = contractor, Labels = labels });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Cash_HasAnyEntries_SeesLinkedAndUnlinkedRowsOfTheOwnerOnly()
    {
        await AddCash(_other, 0, _jan1, 5);
        await AddCash(_owner, null, _jan1, 5); // no currency: not part of the pooled ledger
        Assert.False(await _cash.HasAnyEntries(_owner, TestContext.Current.CancellationToken));

        await AddCash(_owner, 0, _jan1, 5);
        await AddCash(_guest, 0, _jan1, 5, account: 7);

        Assert.True(await _cash.HasAnyEntries(_owner, TestContext.Current.CancellationToken));
        Assert.True(await _cash.HasAnyEntries(_guest, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _cash.HasAnyEntries(0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Bond_HasAnyEntries_SeesLinkedAndUnlinkedRowsOfTheOwnerOnly()
    {
        _context.BondEntries.Add(new BondAccountEntry(null, 0, _jan1, 0, 1, 5) { UserId = _other });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False(await _bonds.HasAnyEntries(_owner, TestContext.Current.CancellationToken));

        _context.BondEntries.Add(new BondAccountEntry(null, 0, _jan1, 0, 1, 5) { UserId = _guest });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await _bonds.HasAnyEntries(_guest, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _bonds.HasAnyEntries(0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cash_GetLabelTotals_SumsEveryLabelOfAnEntry_InsideTheInclusiveWindow_ForTheOwnerOnly()
    {
        await AddCash(_owner, 0, _jan1, 100, labels: [_salary]);
        await AddCash(_owner, 0, _jan1.AddDays(1), -40, account: 9, labels: [_rent, _salary]);
        await AddCash(_owner, 0, _jan1.AddDays(2), -7); // no label
        await AddCash(_owner, 0, _jan1.AddDays(5), 1000, labels: [_salary]); // on the window end: counts
        await AddCash(_owner, 0, _jan1.AddDays(6), 5000, labels: [_salary]); // after the window
        await AddCash(_owner, null, _jan1, 5000, labels: [_salary]); // no currency
        await AddCash(_other, 0, _jan1, 9999, labels: [_salary]);

        var totals = await _cash.GetLabelTotals(_owner, _jan1, _jan1.AddDays(5), TestContext.Current.CancellationToken);

        Assert.Equal(2, totals.Count);
        Assert.Equal(1060m, totals[_salary.Id]);
        Assert.Equal(-40m, totals[_rent.Id]);
        Assert.Empty(await _cash.GetLabelTotals(_owner, _jan1.AddDays(5), _jan1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cash_GetLabelTotals_AcceptsGuestIds_AndRejectsZero()
    {
        await AddCash(_guest, 0, _jan1, 12, labels: [_rent]);

        Assert.Equal(12m, (await _cash.GetLabelTotals(_guest, _jan1, _jan1, TestContext.Current.CancellationToken))[_rent.Id]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _cash.GetLabelTotals(0, _jan1, _jan1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cash_GetLabeledEntries_HonoursTheEndBound_OutflowFilter_LabelFilter_AndCurrencyZero()
    {
        await AddCash(_owner, 0, _jan1, 100, labels: [_salary]); // PLN is currency id 0
        await AddCash(_owner, 2, _jan1.AddDays(1), -40, account: 9, labels: [_rent, _salary]);
        await AddCash(_owner, 0, _jan1.AddDays(2), -7);
        await AddCash(_owner, 0, _jan1.AddDays(3), 60, labels: [_salary]);
        await AddCash(_owner, null, _jan1.AddDays(1), 8000, labels: [_salary]);
        await AddCash(_other, 0, _jan1.AddDays(1), 9999, labels: [_salary]);
        var ct = TestContext.Current.CancellationToken;

        var exclusive = await _cash.GetLabeledEntries(_owner, _jan1, _jan1.AddDays(3), ct: ct);
        var inclusive = await _cash.GetLabeledEntries(_owner, _jan1, _jan1.AddDays(3), endInclusive: true, ct: ct);
        var outflows = await _cash.GetLabeledEntries(_owner, _jan1, _jan1.AddDays(3), endInclusive: true, outflowsOnly: true, ct: ct);
        var salaryOnly = await _cash.GetLabeledEntries(_owner, _jan1, _jan1.AddDays(3), endInclusive: true, labelId: _salary.Id, ct: ct);

        Assert.Equal([-40m, -7m, 100m], exclusive.Select(e => e.ValueChange).Order());
        Assert.Equal(4, inclusive.Count);
        Assert.Equal([-40m, -7m], outflows.Select(e => e.ValueChange).Order());
        Assert.Equal(3, salaryOnly.Count);
        // The entry keeps all of its labels, not only the one filtered on; entries without a label carry an empty list.
        Assert.Equal(new[] { _rent.Id, _salary.Id }.Order(), salaryOnly.Single(e => e.ValueChange == -40m).LabelIds.Order());
        Assert.Equal(2, salaryOnly.Single(e => e.ValueChange == -40m).CurrencyId);
        Assert.Equal(0, salaryOnly.Single(e => e.ValueChange == 100m).CurrencyId);
        Assert.Empty(inclusive.Single(e => e.ValueChange == -7m).LabelIds);
        Assert.Empty(await _cash.GetLabeledEntries(_owner, _jan1.AddDays(3), _jan1, ct: ct));
    }

    [Fact]
    public async Task Cash_GetLabeledEntries_AcceptsGuestIds_AndRejectsZero()
    {
        await AddCash(_guest, 0, _jan1, 5, labels: [_salary]);

        Assert.Single(await _cash.GetLabeledEntries(_guest, _jan1, _jan1, endInclusive: true, ct: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _cash.GetLabeledEntries(0, _jan1, _jan1, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cash_GetRecurringCandidates_ReturnsNonZeroEntriesInHalfOpenWindow_WithAccountOrNull()
    {
        await AddCash(_owner, 0, _jan1, -40, account: 9, description: "Rent", contractor: "Landlord");
        await AddCash(_owner, 2, _jan1.AddDays(1), 500, description: "Salary");
        await AddCash(_owner, 0, _jan1.AddDays(2), 0, description: "Zero");
        await AddCash(_owner, 0, _jan1.AddDays(3), -1, description: "On the end");
        await AddCash(_owner, null, _jan1.AddDays(1), -9, description: "No currency");
        await AddCash(_other, 0, _jan1.AddDays(1), -9999, description: "Someone else");

        var candidates = await _cash.GetRecurringCandidates(_owner, _jan1, _jan1.AddDays(3), TestContext.Current.CancellationToken);

        Assert.Equal(["Rent", "Salary"], candidates.OrderBy(c => c.PostingDate).Select(c => c.Description));
        var rent = candidates.Single(c => c.Description == "Rent");
        Assert.Equal(9, rent.AccountId);
        Assert.Equal("Landlord", rent.ContractorDetails);
        Assert.Null(candidates.Single(c => c.Description == "Salary").AccountId);
        Assert.Empty(await _cash.GetRecurringCandidates(_owner, _jan1.AddDays(3), _jan1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _cash.GetRecurringCandidates(0, _jan1, _jan1.AddDays(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Investments_GetCountPerUser_CountsLinkedAndUnlinkedTradesInOneGroupedQuery_IncludingGuests()
    {
        _context.Assets.Add(new Asset { Id = 1, Name = "Equity", Type = AssetType.Stock });
        _context.AssetListings.Add(new AssetListing { Id = 11, AssetId = 1, Ticker = "AAA", ExchangeMic = "XNAS", ExchangeName = "N", TradingCurrency = "USD", PriceMultiplier = 1m, IsActive = true });
        InvestmentTransaction Trade(long user, int? account) => new()
        {
            UserId = user,
            AccountId = account,
            AssetListingId = 11,
            Type = InvestmentTransactionType.Buy,
            Quantity = 1,
            UnitPrice = 1m,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(_jan1),
            PostingDate = _jan1,
        };
        _context.InvestmentTransactions.AddRange(Trade(_owner, 4), Trade(_owner, null), Trade(_owner, null), Trade(_guest, null), Trade(_other, null));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var counts = await _investments.GetCountPerUser([_owner, _guest, 99, 0, _owner], TestContext.Current.CancellationToken);

        Assert.Equal(2, counts.Count);
        Assert.Equal(3, counts[_owner]);
        Assert.Equal(1, counts[_guest]);
        Assert.Empty(await _investments.GetCountPerUser([], TestContext.Current.CancellationToken));
        Assert.Empty(await _investments.GetCountPerUser([0], TestContext.Current.CancellationToken));
    }
}