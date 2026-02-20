namespace FinanceManager.Domain.Dtos;

/// <summary>
/// Represents the mapping suggestion for a CSV header.
/// Shows the original header name and the field it should map to.
/// </summary>
public record HeaderMappingResultDto(string OriginalHeaderName, string MappedFieldName);
