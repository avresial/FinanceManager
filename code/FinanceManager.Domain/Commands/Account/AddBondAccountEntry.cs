using FinanceManager.Domain.Validation;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.Account;

public record AddBondAccountEntry(
    [Range(1, int.MaxValue)] int AccountId,
    [Range(0, int.MaxValue)] int EntryId,
    [ReasonableDate] DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    [Range(1, int.MaxValue)] int BondDetailsId);