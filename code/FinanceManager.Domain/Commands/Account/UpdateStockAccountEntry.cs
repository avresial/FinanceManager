using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Validation;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.Account;

public record UpdateStockAccountEntry(
    [Range(1, int.MaxValue)] int AccountId,
    [Range(1, int.MaxValue)] int EntryId,
    [ReasonableDate] DateTime PostingDate,
    decimal Value,
    decimal ValueChange,
    [Required, StringLength(32)] string Ticker,
    InvestmentType InvestmentType,
    List<UpdateFiancialLabel> Labels);