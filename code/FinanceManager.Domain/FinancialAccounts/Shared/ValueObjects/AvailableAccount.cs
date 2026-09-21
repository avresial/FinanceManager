using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;

public record AvailableAccount(int AccountId, string AccountName, AccountLabel? AccountLabel = null);