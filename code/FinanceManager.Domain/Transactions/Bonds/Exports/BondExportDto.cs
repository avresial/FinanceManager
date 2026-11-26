using FinanceManager.Domain.Transactions.Export;
namespace FinanceManager.Domain.Transactions.Bonds.Exports;

public record BondExportDto(
    long Id,
    DateTime PostingDate,
    decimal ValueChange,
    string BondName,
    string? Labels = null,
    string Description = "") : ICsvExportDto;