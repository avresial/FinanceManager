namespace FinanceManager.Domain.Entities.Exports;

public record BondAccountExportDto(
    DateTime PostingDate,
    decimal ValueChange,
    int BondDetailsId) : IAccountExportDto;