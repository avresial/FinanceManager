using FinanceManager.Domain.Entities.Stocks;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.Account;

public record AddStockAccountEntry(
    [Required] StockAccountEntry Entry);