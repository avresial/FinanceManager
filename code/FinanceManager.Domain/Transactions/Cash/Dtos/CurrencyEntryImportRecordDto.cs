using FinanceManager.Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Transactions.Cash.Dtos;

public record CurrencyEntryImportRecordDto(
    [ReasonableDate] DateTime PostingDate,
    decimal ValueChange,
    [StringLength(512)] string? ContractorDetails = null,
    [StringLength(512)] string? Description = null);