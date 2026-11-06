using FinanceManager.Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Commands;

public record AddBondAccountEntry(
    [Range(1, int.MaxValue)] int AccountId,
    [Range(0, long.MaxValue)] long EntryId,
    [ReasonableDate] DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    [Range(1, int.MaxValue)] int BondDetailsId);