using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

internal class FinancialLabelsRepository(AppDbContext context) : IFinancialLabelsRepository
{
    public async Task<bool> Add(string name, CancellationToken cancellationToken = default)
    {
        context.FinancialLabels.Add(new FinancialLabel() { Name = name });
        return await context.SaveChangesAsync(cancellationToken) == 1;
    }

    public async Task<bool> Delete(int id, CancellationToken cancellationToken = default)
    {
        var elementToRemove = await context.FinancialLabels.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (elementToRemove is null) return false;
        context.FinancialLabels.Remove(elementToRemove);
        return await context.SaveChangesAsync(cancellationToken) == 1;
    }

    public Task<int> GetCount(CancellationToken cancellationToken = default) => context.FinancialLabels.AsNoTracking().CountAsync(cancellationToken);
    public IAsyncEnumerable<FinancialLabel> GetLabels(CancellationToken cancellationToken = default) =>
        context.FinancialLabels.AsNoTracking().Include(x => x.Classifications).AsAsyncEnumerable();

    public IAsyncEnumerable<FinancialLabel> GetLabelsByAccountId(int accountId, CancellationToken cancellationToken = default) =>
        context.CurrencyEntries.AsNoTracking().Where(x => x.AccountId == accountId)
            .SelectMany(x => x.Labels)
            .Include(x => x.Classifications)
            .Distinct()
            .AsAsyncEnumerable();

    public Task<FinancialLabel> GetLabelsById(int id, CancellationToken cancellationToken = default) =>
        context.FinancialLabels.AsNoTracking().Include(x => x.Classifications).SingleAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> UpdateName(int id, string name, CancellationToken cancellationToken = default)
    {
        var element = await context.FinancialLabels.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (element is null) return false;
        element.Name = name;

        return await context.SaveChangesAsync(cancellationToken) == 1;
    }

    public async Task<bool> AddClassification(int labelId, string kind, string value, CancellationToken cancellationToken = default)
    {
        var label = await context.FinancialLabels
            .Include(x => x.Classifications)
            .SingleAsync(x => x.Id == labelId, cancellationToken);

        var existing = label.Classifications.SingleOrDefault(x => x.Kind == kind);
        if (existing is null)
        {
            label.Classifications.Add(new FinancialLabelClassification
            {
                LabelId = labelId,
                Kind = kind,
                Value = value
            });

            return await context.SaveChangesAsync(cancellationToken) > 0;
        }

        if (existing.Value == value)
            return true;

        existing.Value = value;
        return await context.SaveChangesAsync(cancellationToken) > 0;
    }
}