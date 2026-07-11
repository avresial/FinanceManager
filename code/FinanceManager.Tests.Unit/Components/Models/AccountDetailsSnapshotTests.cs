using FinanceManager.Components.Models;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Models;

[Trait("Category", "Unit")]
public class AccountDetailsSnapshotTests
{
    // Mirrors the web defaults Blazored.LocalStorage uses so the round-trip matches runtime behaviour.
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Snapshot_RoundTrips_EntriesAndMetadataIntact()
    {
        var account = new CurrencyAccount(7, 42, "Main", AccountLabel.Cash);
        account.AddEntry(new AddCurrencyEntryDto(new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 100m, "Salary", null, []));
        account.AddEntry(new AddCurrencyEntryDto(new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc), -30m, "Groceries", null, []));

        var snapshot = new AccountDetailsSnapshot<CurrencyAccountEntry>
        {
            UserId = account.UserId,
            AccountId = account.AccountId,
            Name = account.Name,
            AccountType = account.AccountType,
            Entries = account.Entries,
        };

        var json = JsonSerializer.Serialize(snapshot, _options);
        var restored = JsonSerializer.Deserialize<AccountDetailsSnapshot<CurrencyAccountEntry>>(json, _options);

        Assert.NotNull(restored);
        Assert.Equal(7, restored.UserId);
        Assert.Equal(42, restored.AccountId);
        Assert.Equal("Main", restored.Name);
        Assert.Equal(AccountLabel.Cash, restored.AccountType);
        Assert.Equal(2, restored.Entries.Count);
        Assert.Equal(
            account.Entries.Select(e => (e.PostingDate, e.ValueChange, e.Value, e.Description)),
            restored.Entries.Select(e => (e.PostingDate, e.ValueChange, e.Value, e.Description)));
    }

    [Fact]
    public void Snapshot_CanRebuildAccount_FromRestoredEntries()
    {
        var account = new CurrencyAccount(7, 42, "Main", AccountLabel.Cash);
        account.AddEntry(new AddCurrencyEntryDto(new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 100m, "Salary", null, []));

        var snapshot = new AccountDetailsSnapshot<CurrencyAccountEntry>
        {
            UserId = account.UserId,
            AccountId = account.AccountId,
            Name = account.Name,
            AccountType = account.AccountType,
            Entries = account.Entries,
        };

        var json = JsonSerializer.Serialize(snapshot, _options);
        var restored = JsonSerializer.Deserialize<AccountDetailsSnapshot<CurrencyAccountEntry>>(json, _options)!;

        // Mirrors the rebuild the page-content component performs when painting a snapshot.
        var rebuilt = new CurrencyAccount(restored.UserId, restored.AccountId, restored.Name, restored.Entries, restored.AccountType);

        Assert.Equal("Main", rebuilt.Name);
        Assert.Equal(AccountLabel.Cash, rebuilt.AccountType);
        Assert.Single(rebuilt.Entries);
        Assert.Equal(100m, rebuilt.Entries[0].Value);
    }
}