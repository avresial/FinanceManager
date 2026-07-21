using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.Dashboard.Dtos;

/// <summary>
/// A single row of the dashboard transaction log: one transaction from any of the
/// user's accounts (currency, bond or investment), normalised to a common shape so
/// the log can interleave entries of every account type in one newest-first list.
/// </summary>
public record TransactionLogEntryDto(
    int AccountId,
    string AccountName,
    AccountType AccountType,
    long EntryId,
    DateTime Date,
    decimal ValueChange,
    string Description);