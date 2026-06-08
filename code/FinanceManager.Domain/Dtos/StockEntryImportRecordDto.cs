using FinanceManager.Domain.Validation;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Dtos;

public record StockEntryImportRecordDto(
    [ReasonableDate] DateTime PostingDate,
    decimal ValueChange,
    [Required, StringLength(32)] string Ticker);