using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Investments;

public class InvestmentTransactionRepository(AppDbContext context) : IInvestmentTransactionRepository
{
    public Task<InvestmentTransaction?> Get(long id, CancellationToken cancellationToken = default) =>
        context.InvestmentTransactions
            .Include(x => x.AssetListing)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByAccount(int accountId, CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.TradeDate).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByUser(long userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.UserId == userId && x.TradeDate >= startDate && x.TradeDate <= endDate)
            .OrderBy(x => x.TradeDate).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<InvestmentTransaction> Add(InvestmentTransaction transaction, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (transaction.CreatedAt == default) transaction.CreatedAt = now;
        transaction.UpdatedAt = now;

        context.InvestmentTransactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    public async Task<bool> Update(InvestmentTransaction transaction, CancellationToken cancellationToken = default)
    {
        var existing = await context.InvestmentTransactions.FirstOrDefaultAsync(x => x.Id == transaction.Id, cancellationToken);
        if (existing is null) return false;

        existing.AssetListingId = transaction.AssetListingId;
        existing.Type = transaction.Type;
        existing.Quantity = transaction.Quantity;
        existing.UnitPrice = transaction.UnitPrice;
        existing.Currency = transaction.Currency;
        existing.TradeDate = transaction.TradeDate;
        existing.Fee = transaction.Fee;
        existing.Notes = transaction.Notes;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        var entity = await context.InvestmentTransactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;

        context.InvestmentTransactions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOf(IReadOnlyCollection<int> accountIds, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return new Dictionary<long, decimal>();

        var rows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.TradeDate <= asOf)
            .Select(x => new { x.AssetListingId, x.Type, x.Quantity })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.AssetListingId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(t => t.Type == InvestmentTransactionType.Sell ? -t.Quantity : t.Quantity));
    }
}