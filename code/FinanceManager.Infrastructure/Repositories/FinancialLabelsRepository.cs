using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

internal class FinancialLabelsRepository(AppDbContext context) : IFinancialLabelsRepository
{
    public async Task<bool> Add(string name)
    {
        context.FinancialLabels.Add(new FinancialLabel() { Name = name });
        return await context.SaveChangesAsync() == 1;
    }

    public async Task<bool> Delete(int id)
    {
        var elementToRemove = context.FinancialLabels.Single(x => x.Id == id);
        context.FinancialLabels.Remove(elementToRemove);
        await context.SaveChangesAsync();
        return true;
    }

    public Task<int> GetCount() => context.FinancialLabels.CountAsync();

    public IAsyncEnumerable<FinancialLabel> GetLabels() => context.FinancialLabels.ToAsyncEnumerable();

    public IAsyncEnumerable<FinancialLabel> GetLabelsByAccountId(int accountId) => context.FinancialLabels
        .Include(x => x.Entries)
        .Where(x => x.Entries.Any(y => y.AccountId == accountId))
        .ToAsyncEnumerable();

    public Task<FinancialLabel> GetLabelsById(int id) => context.FinancialLabels.SingleAsync(x => x.Id == id);

    public async Task<bool> UpdateName(int id, string name)
    {
        var elementToRemove = context.FinancialLabels.Single(x => x.Id == id);
        elementToRemove.Name = name;

        return await context.SaveChangesAsync() == 1;
    }
}