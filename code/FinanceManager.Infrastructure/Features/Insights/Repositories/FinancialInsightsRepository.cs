using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Insights.Repositories;

public class FinancialInsightsRepository(AppDbContext context) : IFinancialInsightsRepository
{
    public Task<int> GetCountByUser(int userId, CancellationToken cancellationToken = default) =>
        context.FinancialInsights.CountAsync(x => x.UserId == userId, cancellationToken);

    // Returns every stored insight of the owner, including historical rows that were generated for one account.
    public async Task<List<FinancialInsight>> GetLatestByUser(int userId, int count, CancellationToken cancellationToken = default) =>
        await context.FinancialInsights.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<bool> AddRange(IEnumerable<FinancialInsight> insights, CancellationToken cancellationToken = default)
    {
        context.FinancialInsights.AddRange(insights);
        return await context.SaveChangesAsync(cancellationToken) > 0;
    }
}