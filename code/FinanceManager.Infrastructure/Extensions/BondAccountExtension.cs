using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;

public static class BondAccountExtension
{
    public static BondAccountDto ToDto(this BondAccount account,
        BondAccountEntry? nextOlderEntry = null,
        BondAccountEntry? nextYoungerEntry = null,
        IEnumerable<BondAccountEntry>? entries = null)
    {
        var effectiveEntries = entries ?? account.Get();
        var older = nextOlderEntry ?? account.NextOlderEntry;
        var younger = nextYoungerEntry ?? account.NextYoungerEntry;

        return new BondAccountDto
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