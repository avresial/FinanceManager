using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
namespace FinanceManager.Domain.FinancialAccounts.Bond.Exports;

public record BondAccountExportDto(
    long Id,
    DateTime PostingDate,
    decimal ValueChange,
    string BondName,
    string? Labels = null,
    string Description = "") : IAccountExportDto;