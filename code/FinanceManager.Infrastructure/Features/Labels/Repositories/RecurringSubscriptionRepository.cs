using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Labels.Repositories;

internal class RecurringSubscriptionRepository(AppDbContext context) : IRecurringSubscriptionRepository
{
    public Task<List<RecurringSubscription>> GetAll(
        int userId,
        CancellationToken cancellationToken = default) =>
        context.RecurringSubscriptions
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task Save(
        IEnumerable<RecurringSubscription> added,
        CancellationToken cancellationToken = default)
    {
        var addedList = added.ToList();
        context.RecurringSubscriptions.AddRange(addedList);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (addedList.Count > 0)
        {
            foreach (var subscription in addedList)
                context.Entry(subscription).State = EntityState.Detached;

            var ids = addedList.Select(x => x.Id).ToList();
            if (await context.RecurringSubscriptions.CountAsync(x => ids.Contains(x.Id), cancellationToken) != ids.Count)
                throw;

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> Update(
        int userId,
        Guid id,
        UpdateRecurringSubscription command,
        CancellationToken cancellationToken = default)
    {
        var subscription = await context.RecurringSubscriptions
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);
        if (subscription is null) return false;

        subscription.IsMuted = command.IsMuted;
        subscription.IsCancelled = command.IsCancelled;
        subscription.IsFlaggedForReview = command.IsFlaggedForReview;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}