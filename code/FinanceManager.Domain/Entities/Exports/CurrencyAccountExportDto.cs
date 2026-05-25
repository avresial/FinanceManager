using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.Domain.Entities.Exports;

public record CurrencyAccountExportDto(
    int Id,
    DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    string? ContractorDetails = null,
    string? Description = null,
    string? Labels = null) : IAccountExportDto
{
    public static CurrencyAccountExportDto FromEntity(CurrencyAccountEntry entry)
    {
        return new(entry.EntryId, entry.PostingDate, entry.Value, entry.ValueChange, entry.ContractorDetails, entry.Description);
    }
}