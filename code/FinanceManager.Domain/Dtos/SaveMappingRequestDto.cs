namespace FinanceManager.Domain.Dtos;

/// <summary>
/// Represents a user's final mapping choice for CSV headers.
/// Sent from the frontend to save the mapping in the database.
/// </summary>
public record SaveMappingRequestDto(IReadOnlyList<HeaderMappingRequestItemDto> Mappings);

/// <summary>
/// A single header-to-field mapping choice.
/// </summary>
public record HeaderMappingRequestItemDto(string HeaderName, string FieldName);
