using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Dtos;

public record BondDataImportDto(
    [Range(1, int.MaxValue)] int AccountId,
    [Required, MinLength(1)] IReadOnlyList<BondEntryImportRecordDto> Entries);
