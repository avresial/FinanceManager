namespace FinanceManager.Domain.FinancialAccounts.Shared.Entities;

/// <summary>Account identity and ownership, independent of its transaction history.</summary>
public interface IAccount
{
    int AccountId { get; }
    int UserId { get; }
    string Name { get; }
}