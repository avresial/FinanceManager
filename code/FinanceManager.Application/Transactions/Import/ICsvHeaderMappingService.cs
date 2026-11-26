using FinanceManager.Domain.Transactions.Import.Dtos;

namespace FinanceManager.Application.Transactions.Import;

public interface ICsvHeaderMappingService
{
    /// <summary>
    /// Get suggested mappings for a list of CSV headers based on global mappings.
    /// </summary>
    Task<List<HeaderMappingResultDto>> GetSuggestedMappingsAsync(IEnumerable<string> headers);

    /// <summary>
    /// Save or update header mappings globally.
    /// </summary>
    Task SaveMappingsAsync(SaveMappingRequestDto mappingRequest);
}