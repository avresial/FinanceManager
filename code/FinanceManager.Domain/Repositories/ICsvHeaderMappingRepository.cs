using FinanceManager.Domain.Entities.Imports;

namespace FinanceManager.Domain.Repositories;

public interface ICsvHeaderMappingRepository
{
    Task<List<CsvHeaderMapping>> GetAllMappings();
    Task<CsvHeaderMapping?> GetMappingByHeader(string headerName, string fieldName);
    Task SaveOrUpdateMappings(IEnumerable<CsvHeaderMapping> mappings);
}
