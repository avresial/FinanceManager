using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.Domain.Entities.Exports;

public record CurrencyAccountExportDto(
    int Id,
    DateTime PostingDate,
    decimal ValueChange,
    string? ContractorDetails = null,
    string? Description = null,
    string? Labels = null) : IAccountExportDto
{
    public static CurrencyAccountExportDto FromEntity(CurrencyAccountEntry entry)
    {
        var labels = entry.Labels.Count > 0 ? string.Join(", ", entry.Labels.Select(l => l.Name)) : null;
        return new(entry.EntryId, entry.PostingDate, entry.ValueChange, entry.ContractorDetails, entry.Description, labels);
    }
}