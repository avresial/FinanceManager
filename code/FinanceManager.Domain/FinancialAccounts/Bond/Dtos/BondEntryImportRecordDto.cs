using FinanceManager.Domain.Validation;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Dtos;

public record BondEntryImportRecordDto(
    [ReasonableDate] DateTime PostingDate,
    decimal ValueChange,
    [Range(1, int.MaxValue)] int BondDetailsId);