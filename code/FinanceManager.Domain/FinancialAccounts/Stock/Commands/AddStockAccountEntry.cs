using FinanceManager.Domain.Commands.Account;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Commands;

public record AddStockAccountEntry(
    [Required] StockAccountEntry Entry);