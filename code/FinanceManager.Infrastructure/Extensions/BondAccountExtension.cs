using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Extensions;

public static class BondAccountExtension
{
    public static BondAccountDto ToDto(this BondAccount account,
        Dictionary<int, BondAccountEntry>? nextOlderEntries = null,
        Dictionary<int, BondAccountEntry>? nextYoungerEntries = null,
        IEnumerable<BondAccountEntry>? entries = null)
    {
        var effectiveEntries = entries ?? account.Get();
        var older = nextOlderEntries ?? account.NextOlderEntries;
        var younger = nextYoungerEntries ?? account.NextYoungerEntries;

        return new BondAccountDto
        {
            AccountId = account.AccountId,
            UserId = account.UserId,
            Name = account.Name,
            AccountLabel = account.AccountType,
            NextOlderEntries = older?.ToDto(),
            NextYoungerEntries = younger?.ToDto(),
            Entries = effectiveEntries.Select(e => e.ToDto()).ToList()
        };
    }
}