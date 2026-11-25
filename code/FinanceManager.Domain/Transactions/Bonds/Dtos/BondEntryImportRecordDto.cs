using FinanceManager.Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Transactions.Bonds.Dtos;

public record BondEntryImportRecordDto(
    [ReasonableDate] DateTime PostingDate,
    decimal ValueChange,
    [Range(1, int.MaxValue)] int BondDetailsId,
    [StringLength(512)] string? Description = null);