using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Gives account-linked rows that tests seed straight through the <see cref="DbContext"/> the owner keys the repositories stamp
/// on every real write: <c>UserId</c> from the account, and <c>CurrencyId</c> from a currency account. Owner-pooled reads
/// (net worth, assets, liabilities, balances) only see rows that carry those keys. Rows that already carry them, and unlinked
/// rows, are left untouched.
/// </summary>
internal sealed class OwnerKeyStampingInterceptor : SaveChangesInterceptor
{
    /// <summary>Turned off once seeding is done, so rows written through the real repositories must carry their own owner keys.</summary>
    public bool Enabled { get; set; } = true;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (Enabled && eventData.Context is AppDbContext context) Stamp(context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Enabled && eventData.Context is AppDbContext context) Stamp(context);
        return ValueTask.FromResult(result);
    }

    private static void Stamp(AppDbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
        {
            switch (entry.Entity)
            {
                case CurrencyAccountEntry { AccountId: int accountId, UserId: null } cash when FindAccount(context, accountId) is FinancialAccountBaseDto account:
                    cash.UserId = account.UserId;
                    cash.CurrencyId ??= account.CurrencyId;
                    break;
                case BondAccountEntry { AccountId: int accountId, UserId: null } bond when FindAccount(context, accountId) is FinancialAccountBaseDto account:
                    bond.UserId = account.UserId;
                    break;
                case InvestmentTransaction { AccountId: int accountId, UserId: 0 } trade when FindAccount(context, accountId) is FinancialAccountBaseDto account:
                    trade.UserId = account.UserId;
                    break;
            }
        }
    }

    private static FinancialAccountBaseDto? FindAccount(AppDbContext context, int accountId) =>
        context.Accounts.Local.FirstOrDefault(a => a.AccountId == accountId)
        ?? context.Accounts.AsNoTracking().FirstOrDefault(a => a.AccountId == accountId);
}