using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.Domain.Entities.Exports;

public record CurrencyAccountExportDto(
    DateTime PostingDate,
    decimal ValueChange,
    string? ContractorDetails = null,
    string? Description = null) : IAccountExportDto
{
    public static CurrencyAccountExportDto FromEntity(CurrencyAccountEntry entry) =>
        new(entry.PostingDate, entry.ValueChange, entry.ContractorDetails, entry.Description);
}