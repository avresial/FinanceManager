using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>One account the Transactions page can be filtered by.</summary>
public sealed record TransactionLogAccountOption(int AccountId, string AccountName, AccountType AccountType);