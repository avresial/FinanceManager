using FinanceManager.Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Dtos;

public record StockEntryImportRecordDto(
    [ReasonableDate] DateTime PostingDate,
    decimal ValueChange,
    [Required, StringLength(32)] string Ticker);