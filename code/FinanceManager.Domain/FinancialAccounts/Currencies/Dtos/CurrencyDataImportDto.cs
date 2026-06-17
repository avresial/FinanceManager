using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;

public record CurrencyDataImportDto(
    [Range(1, int.MaxValue)] int AccountId,
    [Required, MinLength(1)] IReadOnlyList<CurrencyEntryImportRecordDto> Entries);