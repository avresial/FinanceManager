using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Transactions.Import.Dtos;

public record SaveMappingRequestDto(
    [Required, MinLength(1)] IReadOnlyList<HeaderMappingRequestItemDto> Mappings);

public record HeaderMappingRequestItemDto(
    [Required, StringLength(256)] string HeaderName,
    [Required, StringLength(256)] string FieldName);