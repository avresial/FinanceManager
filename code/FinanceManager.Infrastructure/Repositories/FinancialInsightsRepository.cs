using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class FinancialInsightsRepository(AppDbContext context) : IFinancialInsightsRepository
{
    public Task<int> GetCountByUser(int userId, CancellationToken cancellationToken = default) =>
        context.FinancialInsights.CountAsync(x => x.UserId == userId, cancellationToken);

    public async Task<List<FinancialInsight>> GetLatestByUser(int userId, int count, int? accountId = null, CancellationToken cancellationToken = default)
    {
        var query = context.FinancialInsights.AsNoTracking().Where(x => x.UserId == userId);
        if (accountId.HasValue)
            query = query.Where(x => x.AccountId == accountId);

        return await query.OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AddRange(IEnumerable<FinancialInsight> insights, CancellationToken cancellationToken = default)
    {
        context.FinancialInsights.AddRange(insights);
        return await context.SaveChangesAsync(cancellationToken) > 0;
    }
}