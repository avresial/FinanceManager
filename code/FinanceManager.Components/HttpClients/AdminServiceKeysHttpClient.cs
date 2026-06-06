using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class AdminServiceKeysHttpClient(HttpClient httpClient)
{
    public async Task<ExternalServicesResponse?> GetConfigurationAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<ExternalServicesResponse>(
            "api/admin/service-keys", ct);

    public async Task UpdateServiceAsync(string serviceName, UpdateServiceKeyRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"api/admin/service-keys/{Uri.EscapeDataString(serviceName)}", request, ct);
        response.EnsureSuccessStatusCode();
    }
}