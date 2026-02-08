using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

public class CurrencyAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange)
    : FinancialEntryBase(accountId, entryId, postingDate, value, valueChange)
{
    public string Description { get; set; } = string.Empty;
    public string? ContractorDetails { get; set; }

    public void Update(CurrencyAccountEntry entry)
    {
        base.Update(entry);

        Description = entry.Description;
        ContractorDetails = entry.ContractorDetails;
    }

    public CurrencyAccountEntry GetCopy() => new(AccountId, EntryId, PostingDate, Value, ValueChange)
    {
        Description = this.Description,
        ContractorDetails = this.ContractorDetails,
    };

    public override string ToString() => $"PostingDate: {PostingDate}, EntryId: {EntryId}, Value: {Value}, ValueChange: {ValueChange}";
}