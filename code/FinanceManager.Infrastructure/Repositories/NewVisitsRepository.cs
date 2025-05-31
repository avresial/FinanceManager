using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;
public class NewVisitsRepository(NewVisitsContext newVisitsContext)
{
    private readonly NewVisitsContext _context = newVisitsContext;

    public async Task<int> GetVisitAsync(DateTime visitDate)
    {
        var visit = await _context.NewVisits
            .FirstOrDefaultAsync(v => v.DateTime.Date == visitDate.Date);

        if (visit is null) return 0;

        return visit.VisitsCount;
    }
    public async Task<bool> AddVisitAsync(DateTime visitDate)
    {
        var visit = await _context.NewVisits
                            .FirstOrDefaultAsync(v => v.DateTime.Date == visitDate.Date);

        if (visit is null)
        {
            _context.NewVisits.Add(new NewVisits
            {
                DateTime = visitDate,
                VisitsCount = 1
            });
        }
        else
        {
            visit.VisitsCount++;
        }

        await _context.SaveChangesAsync();

        return true;
    }
}
