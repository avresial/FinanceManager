using FinanceManager.Domain.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

public interface ICsvHeaderMappingRepository
{
    Task<List<CsvHeaderMapping>> GetAllMappings();
    Task<CsvHeaderMapping?> GetMappingByHeader(string headerName, string fieldName);
    Task SaveOrUpdateMappings(IEnumerable<CsvHeaderMapping> mappings);
}