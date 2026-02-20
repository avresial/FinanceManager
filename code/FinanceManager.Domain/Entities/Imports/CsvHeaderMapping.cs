namespace FinanceManager.Domain.Entities.Imports;

/// <summary>
/// Represents a mapping between a CSV header name and a standard field name.
/// Used to auto-populate field mappings when importing CSV data.
/// </summary>
public class CsvHeaderMapping
{
    public int Id { get; set; }
    public required string HeaderName { get; set; }
    public required string FieldName { get; set; } // PostingDate, ValueChange, ContractorDetails, Description
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
