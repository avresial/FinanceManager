using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;

public static class BankAccountExtension
{
    /// <summary>
    /// Maps a BankAccount domain entity (optionally with overridden entry data) to a BankAccountDto.
    /// </summary>
    /// <param name="account">Source account.</param>
    /// <param name="nextOlderEntry">Optional older entry (falls back to account.NextOlderEntry if null).</param>
    /// <param name="nextYoungerEntry">Optional younger entry (falls back to account.NextYoungerEntry if null).</param>
    /// <param name="entries">Optional explicit entries collection (falls back to account.Get()).</param>
    public static BankAccountDto ToDto(this BankAccount account,
        BankAccountEntry? nextOlderEntry = null,
        BankAccountEntry? nextYoungerEntry = null,
        IEnumerable<BankAccountEntry>? entries = null)
    {
        var effectiveEntries = entries ?? account.Get();
        var older = nextOlderEntry ?? account.NextOlderEntry;
        var younger = nextYoungerEntry ?? account.NextYoungerEntry;

        return new BankAccountDto
        {
            AccountId = account.AccountId,
            UserId = account.UserId,
            Name = account.Name,
            AccountLabel = account.AccountType,
            NextOlderEntry = older?.ToDto(),
            NextYoungerEntry = younger?.ToDto(),
            Entries = effectiveEntries.Select(e => e.ToDto()).ToList()
        };
    }
}
