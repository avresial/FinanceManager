using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Validation;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.Account;

public record UpdateCurrencyAccountEntry(
    [Range(1, int.MaxValue)] int AccountId,
    [Range(1, int.MaxValue)] int EntryId,
    [ReasonableDate] DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    [Required, StringLength(512)] string Description,
    [StringLength(512)] string? ContractorDetails,
    List<UpdateFiancialLabel> Labels);
