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
    public IAsyncEnumerable<FinancialLabel> GetLabels(CancellationToken cancellationToken = default) => context.FinancialLabels.ToAsyncEnumerable();

    public IAsyncEnumerable<FinancialLabel> GetLabelsByAccountId(int accountId, CancellationToken cancellationToken = default) => context.BankEntries.Where(x => x.AccountId == accountId)
        .SelectMany(x => x.Labels)
        .Distinct()
        .ToAsyncEnumerable();

    public Task<FinancialLabel> GetLabelsById(int id, CancellationToken cancellationToken = default) => context.FinancialLabels.SingleAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> UpdateName(int id, string name, CancellationToken cancellationToken = default)
    {
        var elementToRemove = context.FinancialLabels.Single(x => x.Id == id);
        elementToRemove.Name = name;

        return await context.SaveChangesAsync(cancellationToken) == 1;
    }
}