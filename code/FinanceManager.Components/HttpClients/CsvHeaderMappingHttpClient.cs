using FinanceManager.Domain.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class CsvHeaderMappingHttpClient(HttpClient httpClient)
{
    public async Task<List<HeaderMappingResultDto>> GetSuggestedMappingsAsync(IEnumerable<string> headers)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{httpClient.BaseAddress}api/CsvHeaderMapping/SuggestMappings",
            headers);

        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<List<HeaderMappingResultDto>>() ?? [];
    }

    public async Task SaveMappingsAsync(SaveMappingRequestDto mappingRequest)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{httpClient.BaseAddress}api/CsvHeaderMapping/SaveMappings",
            mappingRequest);

        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());
    }
}
