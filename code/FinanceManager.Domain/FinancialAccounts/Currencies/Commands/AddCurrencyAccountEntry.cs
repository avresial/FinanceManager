using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Commands;

public record AddCurrencyAccountEntry(
    [Range(1, int.MaxValue)] int AccountId,
    [Range(1, int.MaxValue)] int EntryId,
    [ReasonableDate] DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    [Required, StringLength(512)] string Description,
    [StringLength(512)] string? ContractorDetails);