
namespace FinanceManager.Domain.Transactions.Import.Repositories;

public interface ICsvHeaderMappingRepository
{
    Task<List<CsvHeaderMapping>> GetAllMappings();
    Task SaveOrUpdateMappings(IEnumerable<CsvHeaderMapping> mappings);
}