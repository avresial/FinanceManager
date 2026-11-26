using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Transactions.Import;
using FinanceManager.Domain.Transactions.Import.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Transactions.Import.Repositories;

public class CsvHeaderMappingRepository(AppDbContext context) : ICsvHeaderMappingRepository
{
    public Task<List<CsvHeaderMapping>> GetAllMappings() =>
        context.CsvHeaderMappings.ToListAsync();

    public async Task SaveOrUpdateMappings(IEnumerable<CsvHeaderMapping> mappings)
    {
        foreach (var mapping in mappings)
        {
            var existingMapping = await context.CsvHeaderMappings
                .FirstOrDefaultAsync(m => m.HeaderName == mapping.HeaderName &&
                                           m.FieldName == mapping.FieldName);

            if (existingMapping is null)
            {
                context.CsvHeaderMappings.Add(mapping);
            }
            else
            {
                existingMapping.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
    }
}