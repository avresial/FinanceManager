using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories;
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
        var elementToRemove = context.FinancialLabels.Single(x => x.Id == id);
        context.FinancialLabels.Remove(elementToRemove);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<int> GetCount(CancellationToken cancellationToken = default) => context.FinancialLabels.CountAsync(cancellationToken);
    public IAsyncEnumerable<FinancialLabel> GetLabels(CancellationToken cancellationToken = default) =>
        context.FinancialLabels.Include(x => x.Classifications).AsAsyncEnumerable();

    public IAsyncEnumerable<FinancialLabel> GetLabelsByAccountId(int accountId, CancellationToken cancellationToken = default) =>
        context.CurrencyEntries.Where(x => x.AccountId == accountId)
            .SelectMany(x => x.Labels)
            .Include(x => x.Classifications)
            .Distinct()
            .AsAsyncEnumerable();

    public Task<FinancialLabel> GetLabelsById(int id, CancellationToken cancellationToken = default) =>
        context.FinancialLabels.Include(x => x.Classifications).SingleAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> UpdateName(int id, string name, CancellationToken cancellationToken = default)
    {
        var elementToRemove = context.FinancialLabels.Single(x => x.Id == id);
        elementToRemove.Name = name;

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