using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.Infrastructure.Extensions;

public static class CurrencyAccountExtension
{
    /// <summary>
    /// Maps a CurrencyAccount domain entity (optionally with overridden entry data) to a CurrencyAccountDto.
    /// </summary>
    /// <param name="account">Source account.</param>
    /// <param name="nextOlderEntry">Optional older entry (falls back to account.NextOlderEntry if null).</param>
    /// <param name="nextYoungerEntry">Optional younger entry (falls back to account.NextYoungerEntry if null).</param>
    /// <param name="entries">Optional explicit entries collection (falls back to account.Get()).</param>
    public static CurrencyAccountDto ToDto(this CurrencyAccount account,
        CurrencyAccountEntry? nextOlderEntry = null,
        CurrencyAccountEntry? nextYoungerEntry = null,
        IEnumerable<CurrencyAccountEntry>? entries = null)
    {
        var effectiveEntries = entries ?? account.Get();
        var older = nextOlderEntry ?? account.NextOlderEntry;
        var younger = nextYoungerEntry ?? account.NextYoungerEntry;

        return new CurrencyAccountDto
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