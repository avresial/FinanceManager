using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Extensions;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Domain.Entities.Accounts;

[Trait("Category", "Unit")]
public class AccountLedgerTests
{
    private static readonly DateTime _date = new(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CurrencyAccount_SerializationPreservesExistingApiPayload()
    {
        var account = new CurrencyAccount(2, 3, "Cash", [new CurrencyAccountEntry(3, 5_000_000_000L, _date, 10m, 2m)], currencyId: 7);
        Assert.Equal(_currencyGolden, JsonSerializer.Serialize(account, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void BondAccount_SerializationPreservesExistingApiPayload()
    {
        var account = new BondAccount(2, 3, "Bonds", [new BondAccountEntry(3, 5_000_000_000L, _date, 10m, 2m, 4)]);
        Assert.Equal(_bondGolden, JsonSerializer.Serialize(account, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void BondAccount_ContainsAssetsCurrentlyUsesOnlyNewestEntry()
    {
        var account = new BondAccount(1, 1, "Mixed");
        account.Add(new BondAccountEntry(1, 1, _date, 0m, 10m, 1));
        account.Add(new BondAccountEntry(1, 2, _date.AddDays(1), 0m, 5m, 2));
        account.Add(new BondAccountEntry(1, 3, _date.AddDays(2), 0m, -5m, 2));

        Assert.Equal(10m, account.Entries.Single(e => e.EntryId == 1).Value);
        // Preserve the existing false negative; correcting asset totals is tracked in #1107.
        Assert.False(account.ContainsAssets);
    }

    [Fact]
    public void BondAccount_AddAndRemoveKeepInstrumentBalancesSeparate()
    {
        var account = new BondAccount(1, 1, "Mixed");
        account.Add(new BondAccountEntry(1, 1, _date, 0m, 10m, 1));
        account.Add(new BondAccountEntry(1, 2, _date.AddDays(1), 0m, 100m, 2));
        account.Add(new BondAccountEntry(1, 3, _date.AddDays(2), 0m, 5m, 1));
        account.Add(new BondAccountEntry(1, 4, _date.AddDays(3), 0m, -20m, 2));

        Assert.Equal(new[] { 80m, 15m, 100m, 10m }, account.Entries.Select(e => e.Value));
        account.Remove(2);
        Assert.Equal(new[] { -20m, 15m, 10m }, account.Entries.Select(e => e.Value));
        account.Remove(long.MaxValue);
        Assert.Equal(3, account.Entries.Count);
    }

    [Fact]
    public void BondAccount_CarriedOnlySlicePreservesBoundsAndNeighbours()
    {
        var carried = new BondAccountEntry(1, 1, _date, 10m, 2m, 4);
        var account = new BondAccount(1, 1, "Carried", [carried], nextOlderEntries: new() { [4] = carried });

        Assert.Equal(_date, account.Start);
        Assert.Equal(_date, account.End);
        Assert.Same(carried, account.GetThisOrNextOlder(_date.AddMonths(1), 4));
        Assert.Empty(account.Get(_date.AddDays(1), _date.AddDays(2)));
    }

    [Fact]
    public void BondAccount_BatchedAndPartialRecalculationKeepInstrumentBalancesSeparate()
    {
        var account = new BondAccount(1, 1, "Mixed");
        account.Add([
            new BondAccountEntry(1, 1, _date, 0m, 10m, 1),
            new BondAccountEntry(1, 2, _date.AddDays(1), 0m, 100m, 2),
            new BondAccountEntry(1, 3, _date.AddDays(2), 0m, 5m, 1),
            new BondAccountEntry(1, 4, _date.AddDays(3), 0m, -20m, 2)
        ], recalculateValues: false);
        Assert.All(account.Entries, entry => Assert.Equal(0m, entry.Value));

        account.RecalculateEntryValues(null);
        Assert.Equal(new[] { 80m, 15m, 100m, 10m }, account.Entries.Select(e => e.Value));

        account.Entries[2].ValueChange = 200m;
        account.RecalculateEntryValues(2);
        Assert.Equal(new[] { 180m, 15m, 200m, 10m }, account.Entries.Select(e => e.Value));
    }

    [Fact]
    public void CurrencyAccount_AddEntryPreservesDuplicatesAndOlderInsertion()
    {
        var account = new CurrencyAccount(1, 1, "Cash");
        var entry = new AddCurrencyEntryDto(_date, 2m, "", null, []);
        account.AddEntry(entry);
        account.AddEntry(entry);
        account.AddEntry(entry with { PostingDate = _date.AddDays(-1), ValueChange = 10m });

        Assert.Equal(new long[] { 1, 0, 2 }, account.Entries.Select(e => e.EntryId));
        Assert.Equal(new[] { 14m, 12m, 10m }, account.Entries.Select(e => e.Value));
        Assert.Equal(_date.AddDays(-1), account.Start);
        Assert.Equal(_date, account.End);
        Assert.Equal(2L, account.GetMaxId());
        Assert.Equal(2, account.Get(_date).Count());
        Assert.Equal(3, account.Get(_date.AddDays(-1), _date).Count());
        account.Remove(long.MaxValue);
        Assert.Equal(3, account.Entries.Count);
    }

    [Fact]
    public void LedgerEntries_PartialRecalculationPreservesOlderOpeningBalance()
    {
        List<FinancialEntryBase> entries = [
            new(1, 3, _date.AddDays(2), 0m, -5m),
            new(1, 2, _date.AddDays(1), 0m, 10m),
            new(1, 1, _date, 100m, 2m)
        ];

        LedgerEntries.RecalculateRunningBalance(entries, 1);

        Assert.Equal(new[] { 105m, 110m, 100m }, entries.Select(e => e.Value));
        Assert.True(LedgerEntries.ContainsEntryId(entries, 2));
        Assert.False(LedgerEntries.ContainsEntryId(entries, 4));
        Assert.Equal(3L, LedgerEntries.GetMaxId(entries));
        Assert.Equal(_date, LedgerEntries.GetStart(entries));
        Assert.Equal(_date.AddDays(2), LedgerEntries.GetEnd(entries));
    }

    [Fact]
    public void EmptyLedgersHaveNoBoundsOrIdentityAndCanBeRecalculated()
    {
        List<FinancialEntryBase> entries = [];
        LedgerEntries.RecalculateRunningBalance(entries, null);
        Assert.Null(LedgerEntries.GetStart(entries));
        Assert.Null(LedgerEntries.GetEnd(entries));
        Assert.Null(LedgerEntries.GetMaxId(entries));

        var bond = new BondAccount(1, 1, "Empty");
        bond.RecalculateEntryValues(null);
        Assert.Null(bond.Start);
        Assert.Null(bond.End);
        Assert.False(bond.ContainsAssets);
    }

    private const string _currencyGolden = """{"accountType":6,"currencyId":7,"start":"2026-05-17T00:00:00Z","end":"2026-05-17T00:00:00Z","entries":[{"description":"","contractorDetails":null,"entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[]}],"containsAssets":true,"accountId":3,"userId":2,"name":"Cash"}""";
    private const string _bondGolden = """{"accountType":6,"start":"2026-05-17T00:00:00Z","end":"2026-05-17T00:00:00Z","entries":[{"bondDetailsId":4,"description":"","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[]}],"containsAssets":true,"accountId":3,"userId":2,"name":"Bonds"}""";
}