using FinanceManager.Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Dtos;

public record BondEntryImportRecordDto(
    [ReasonableDate] DateTime PostingDate,
    decimal ValueChange,
    [Range(1, int.MaxValue)] int BondDetailsId);