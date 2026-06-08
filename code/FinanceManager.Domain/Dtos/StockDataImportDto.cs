using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Dtos;

public record StockDataImportDto(
    [Range(1, int.MaxValue)] int AccountId,
    [Required, MinLength(1)] IReadOnlyList<StockEntryImportRecordDto> Entries);