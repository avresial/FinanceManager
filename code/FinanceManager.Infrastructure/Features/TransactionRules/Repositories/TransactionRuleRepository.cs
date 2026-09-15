using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Domain.TransactionRules.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
        if (!context.Database.IsRelational())
            return await AddCore(rule, cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await AddCore(rule, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
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

        var originalOrders = rules.ToDictionary(rule => rule.Id, rule => rule.Order);
        if (!context.Database.IsRelational())
        {
            SetFinalOrders(rules, orderedIds);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            for (var index = 0; index < rules.Count; index++)
                rules[index].Order = -(index + 1);
            await context.SaveChangesAsync(cancellationToken);

            SetFinalOrders(rules, orderedIds);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var rule in rules)
                rule.Order = originalOrders[rule.Id];
            throw;
        }
    }

    private async Task<TransactionRuleDefinition> AddCore(TransactionRuleDefinition rule, CancellationToken cancellationToken)
    {
        var maxOrder = await context.TransactionRules
            .Where(existing => existing.UserId == rule.UserId)
            .Select(existing => (int?)existing.Order)
            .MaxAsync(cancellationToken) ?? 0;
        rule.Order = maxOrder + 1;
        context.TransactionRules.Add(rule);
        await context.SaveChangesAsync(cancellationToken);
        return rule;
    }

    private static void SetFinalOrders(IReadOnlyList<TransactionRuleDefinition> rules, IReadOnlyList<Guid> orderedIds)
    {
        var byId = rules.ToDictionary(rule => rule.Id);
        for (var index = 0; index < orderedIds.Count; index++)
            byId[orderedIds[index]].Order = index + 1;
    }
}