namespace FinanceManager.Domain.Entities.Exports;

public record CurrencyAccountExportDto(
    DateTime PostingDate,
    decimal ValueChange,
    string? ContractorDetails = null,
    string? Description = null);