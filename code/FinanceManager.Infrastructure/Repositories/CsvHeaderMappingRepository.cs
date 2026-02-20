using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class CsvHeaderMappingRepository(AppDbContext context) : ICsvHeaderMappingRepository
{
    public Task<List<CsvHeaderMapping>> GetAllMappings() =>
        context.CsvHeaderMappings.ToListAsync();

    public Task<CsvHeaderMapping?> GetMappingByHeader(string headerName, string fieldName) =>
        context.CsvHeaderMappings.FirstOrDefaultAsync(m => m.HeaderName == headerName &&
                                       m.FieldName == fieldName);

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
