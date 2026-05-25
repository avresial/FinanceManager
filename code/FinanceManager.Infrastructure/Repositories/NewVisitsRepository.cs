using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class NewVisitsRepository(AppDbContext context) : INewVisitsRepository
{
    public async Task<int> GetVisitAsync(DateTime visitDate)
    {
        var visit = await context.NewVisits
            .FirstOrDefaultAsync(v => v.DateTime.Date == visitDate.Date);

        if (visit is null) return 0;

        return visit.VisitsCount;
    }
    public async Task<bool> AddVisitAsync(DateTime visitDate)
    {
        var visit = await context.NewVisits.FirstOrDefaultAsync(v => v.DateTime.Date == visitDate.Date);

        if (visit is null)
        {
            context.NewVisits.Add(new()
            {
                DateTime = visitDate,
                VisitsCount = 1
            });
        }
        else
        {
            visit.VisitsCount++;
        }

        await context.SaveChangesAsync();

        return true;
    }
}