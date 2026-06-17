using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Commands;

public sealed record AddStockRequest(
    [Required, StringLength(32)] string Ticker,
    [Required, StringLength(256)] string Name,
    [Required, StringLength(64)] string Type,
    [Required, StringLength(64)] string Region,
    [Required, StringLength(3, MinimumLength = 3)] string Currency);