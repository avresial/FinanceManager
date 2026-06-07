using FinanceManager.Domain.Validation;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Dtos;

public record CurrencyEntryImportRecordDto(
    [ReasonableDate] DateTime PostingDate,
    decimal ValueChange,
    [StringLength(512)] string? ContractorDetails = null,
    [StringLength(512)] string? Description = null);
