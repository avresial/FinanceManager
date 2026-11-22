using FinanceManager.Contracts.FinancialAccounts.Bond;
using FinanceManager.Contracts.FinancialAccounts.Currencies;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

/// <summary>Turns wire entries back into the domain entries the account rows and editors work with.</summary>
internal static class AccountEntryMapping
{
    public static CurrencyAccountEntry ToDomain(CurrencyAccountEntryDto entry) =>
        new(entry.AccountId, entry.EntryId, entry.PostingDate, entry.Value, entry.ValueChange)
        {
            Description = entry.Description,
            ContractorDetails = entry.ContractorDetails,
            Labels = entry.Labels?.Select(FinancialLabelMapping.ToDomain).ToList()!
        };

    public static BondAccountEntry ToDomain(BondAccountEntryDto entry) =>
        new(entry.AccountId, entry.EntryId, entry.PostingDate, entry.Value, entry.ValueChange, entry.BondDetailsId)
        {
            Description = entry.Description,
            Labels = entry.Labels?.Select(FinancialLabelMapping.ToDomain).ToList()!
        };
}