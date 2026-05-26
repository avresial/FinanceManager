namespace FinanceManager.Domain.Entities.Exports;

public record BondAccountExportDto(
    int Id,
    DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    string BondName,
    string? Labels = null) : IAccountExportDto;