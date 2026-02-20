namespace FinanceManager.Domain.Entities.Exports;

public record BondAccountExportDto(
    int Id,
    DateTime PostingDate,
    decimal ValueChange,
    int BondDetailsId,
    string? Labels = null) : IAccountExportDto;