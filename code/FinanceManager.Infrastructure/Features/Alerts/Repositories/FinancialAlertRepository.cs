using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Alerts.Repositories;

internal sealed class FinancialAlertRepository(AppDbContext context) : IFinancialAlertRepository
{
    public async Task<IReadOnlyList<FinancialAlert>> GetAlertsByUserId(
        int userId,
        CancellationToken cancellationToken = default) =>
        await context.FinancialAlerts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<FinancialAlert?> GetById(
        int userId,
        Guid alertId,
        CancellationToken cancellationToken = default) =>
        context.FinancialAlerts
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == alertId, cancellationToken);

    public async Task<FinancialAlert> Add(
        FinancialAlert alert,
        CancellationToken cancellationToken = default)
    {
        context.FinancialAlerts.Add(alert);
        await context.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public async Task Update(
        FinancialAlert alert,
        CancellationToken cancellationToken = default)
    {
        context.FinancialAlerts.Update(alert);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Delete(
        int userId,
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await context.FinancialAlerts
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == alertId, cancellationToken);
        if (alert is null)
            return false;

        context.FinancialAlerts.Remove(alert);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}