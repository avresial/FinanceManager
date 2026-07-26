using FinanceManager.Domain.Assets.Discovery;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

public class InvestmentInstrumentDiscoveryHttpClient(HttpClient httpClient)
{
    private const string _base = "api/investments/instruments";

    public async Task<IReadOnlyList<InstrumentDiscoveryResultDto>> SearchAsync(string query, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<List<InstrumentDiscoveryResultDto>>($"{_base}/search?query={Uri.EscapeDataString(query)}", ct) ?? [];

    public async Task<InstrumentImportPreviewDto?> PreviewAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{_base}/import-preview", instrument, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InstrumentImportPreviewDto>(cancellationToken: ct);
    }

    public async Task<ImportedInstrumentDto?> ImportAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{_base}/import", new ImportInstrumentCommand(instrument), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportedInstrumentDto>(cancellationToken: ct);
    }
}