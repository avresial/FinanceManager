using CsvHelper.Configuration.Attributes;

namespace FinanceManager.Domain.Entities.Exports;

public record CurrencyAccountExportDto(
    [property: Index(0), Name("PostingDate"), Format("O")] DateTime PostingDate,
    [property: Index(1), Name("ValueChange")] decimal ValueChange,
    [property: Index(2), Name("ContractorDetails")] string? ContractorDetails = null,
    [property: Index(3), Name("Description")] string? Description = null);