using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;
public class NewVisitsRepository(AppDbContext context)
{
    private readonly AppDbContext _dbContext = context;

    public async Task<int> GetVisitAsync(DateTime visitDate)
    {
        var visit = await _dbContext.NewVisits
            .FirstOrDefaultAsync(v => v.DateTime.Date == visitDate.Date);

        if (visit is null) return 0;

        return visit.VisitsCount;
    }
    public async Task<bool> AddVisitAsync(DateTime visitDate)
    {
        var visit = await _dbContext.NewVisits
                            .FirstOrDefaultAsync(v => v.DateTime.Date == visitDate.Date);

        if (visit is null)
        {
            _dbContext.NewVisits.Add(new NewVisits
            {
                DateTime = visitDate,
                VisitsCount = 1
            });
        }
        else
        {
            visit.VisitsCount++;
        }

        await _dbContext.SaveChangesAsync();

        return true;
    }
}
