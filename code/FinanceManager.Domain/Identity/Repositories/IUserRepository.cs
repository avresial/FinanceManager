using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Identity.Repositories;

public interface IUserRepository
{
    Task<int> GetUsersCount();
    Task<User?> GetUser(string login, string password);
    Task<User?> GetUser(string login);
    Task<User?> GetUser(int id);
    IAsyncEnumerable<User> GetUsers(DateTime startDate, DateTime endDate);
    IAsyncEnumerable<User> GetUsers(int recordIndex, int recordsCount);
    IAsyncEnumerable<int> GetUsersIds(int recordIndex, int recordsCount);
    Task<bool> UpdatePassword(int userId, string password);
    Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel);
    Task<bool> UpdatePreferredCurrency(int userId, int currencyId);
    Task<bool> UpdateRole(int userId, UserRole userRole);
    Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole, string? firstName = null, string? lastName = null);

    /// <summary>
    /// Reads the persisted login-throttling state for an account, or <c>null</c> when no such account exists.
    /// Returning <c>null</c> for unknown logins lets callers avoid tracking (and thereby enumerating) accounts
    /// that were never registered.
    /// </summary>
    Task<LoginThrottlingState?> GetLoginThrottlingState(string login);

    /// <summary>
    /// Atomically increments the consecutive failed-login counter for an account and returns the new value, or
    /// <c>null</c> when the account does not exist. The increment must be a single, race-free operation so that
    /// concurrent failed logins cannot lose updates and delay the lockout.
    /// </summary>
    Task<int?> IncrementFailedLoginAttempts(string login);

    /// <summary>
    /// Locks an account until <paramref name="lockoutEndUtc"/> and clears its failed-attempt counter, so that once
    /// the lockout lapses a fresh threshold of failures is required to lock it again. Returns <c>false</c> when the
    /// account does not exist.
    /// </summary>
    Task<bool> LockAccount(string login, DateTime lockoutEndUtc);

    /// <summary>
    /// Clears the failed-attempt counter and any active lockout for an account. Returns <c>false</c> when the
    /// account does not exist.
    /// </summary>
    Task<bool> ResetLoginThrottling(string login);

    /// <summary>
    /// Inserts a user with an explicit id. Intended for ephemeral guest sandboxes where the id is chosen by the
    /// session store ahead of any persistence and cannot be reassigned by an identity column.
    /// </summary>
    Task<bool> AddUserWithId(int userId, string login, string password, PricingLevel pricingLevel, UserRole userRole, string? firstName = null, string? lastName = null);

    Task<bool> RemoveUser(int userId);
}