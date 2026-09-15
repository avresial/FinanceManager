using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Domain.TransactionRules.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.TransactionRules.Repositories;

internal sealed class TransactionRuleRepository(AppDbContext context) : ITransactionRuleRepository
{
    public async Task<IReadOnlyList<TransactionRuleDefinition>> GetByUserId(int userId, CancellationToken cancellationToken = default) =>
        await context.TransactionRules
            .AsNoTracking()
            .Where(rule => rule.UserId == userId)
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);

    public Task<TransactionRuleDefinition?> GetById(int userId, Guid id, CancellationToken cancellationToken = default) =>
        context.TransactionRules.SingleOrDefaultAsync(rule => rule.UserId == userId && rule.Id == id, cancellationToken);

    public async Task<TransactionRuleDefinition> Add(TransactionRuleDefinition rule, CancellationToken cancellationToken = default)
    {
        context.TransactionRules.Add(rule);
        await context.SaveChangesAsync(cancellationToken);
        return rule;
    }

    public async Task Update(TransactionRuleDefinition rule, CancellationToken cancellationToken = default)
    {
        context.TransactionRules.Update(rule);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Delete(int userId, Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await context.TransactionRules.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);
        if (rule is null) return false;
        context.TransactionRules.Remove(rule);
        await context.SaveChangesAsync(cancellationToken);

        var remaining = await context.TransactionRules
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        for (var index = 0; index < remaining.Count; index++)
            remaining[index].Order = index + 1;
        if (remaining.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> Reorder(int userId, IReadOnlyList<Guid> orderedIds, CancellationToken cancellationToken = default)
    {
        var rules = await context.TransactionRules
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        if (rules.Count != orderedIds.Count || rules.Select(x => x.Id).Intersect(orderedIds).Count() != rules.Count)
            return false;

        for (var index = 0; index < rules.Count; index++)
            rules[index].Order = -(index + 1);
        await context.SaveChangesAsync(cancellationToken);

        var byId = rules.ToDictionary(x => x.Id);
        for (var index = 0; index < orderedIds.Count; index++)
            byId[orderedIds[index]].Order = index + 1;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}