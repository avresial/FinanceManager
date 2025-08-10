using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;
internal class FinancialLabelsRepository(AppDbContext context) : IFinancialLabelsRepository
{
    public Task<List<FinancialLabel>> GetLabels() => context.FinancialLabels.Include(x => x.Entries).ToListAsync();
}
