using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Export;

namespace FinanceManager.Domain.Transactions.Cash.Exports;

public record CashExportDto(
    long Id,
    DateTime PostingDate,
    decimal ValueChange,
    string? ContractorDetails = null,
    string? Description = null,
    string? Labels = null) : ICsvExportDto
{
    public static CashExportDto FromEntity(CashEntry entry)
    {
        return new(entry.EntryId, entry.PostingDate, entry.ValueChange, entry.ContractorDetails, entry.Description);
    }
}