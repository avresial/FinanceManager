using FinanceManager.Domain.Entities.Exports;
namespace FinanceManager.Domain.FinancialAccounts.Bond.Exports;

public record BondAccountExportDto(
    int Id,
    DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    string BondName,
    string? Labels = null) : IAccountExportDto;