using FinanceManager.Domain.Entities.Stocks;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Application.Commands.Account;

public record AddStockAccountEntry(
    [Required] StockAccountEntry Entry);