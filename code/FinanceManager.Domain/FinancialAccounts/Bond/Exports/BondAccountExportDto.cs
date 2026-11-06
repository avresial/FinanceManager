using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
namespace FinanceManager.Domain.FinancialAccounts.Bond.Exports;

public record BondAccountExportDto(
    long Id,
    DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    string BondName,
    string? Labels = null) : IAccountExportDto;