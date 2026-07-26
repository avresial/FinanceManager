using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Administration.HttpClients;

public class AdminMaintenanceKeyHttpClient(HttpClient httpClient)
{
    public async Task<MaintenanceKeyStatusResponse?> GetStatusAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<MaintenanceKeyStatusResponse>("api/admin/maintenance-key", ct);

    public async Task<GeneratedMaintenanceKeyResponse?> GenerateAsync(CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("api/admin/maintenance-key", content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GeneratedMaintenanceKeyResponse>(ct);
    }

    /// <summary>Returns <c>false</c> when there was no key to revoke.</summary>
    public async Task<bool> RevokeAsync(CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync("api/admin/maintenance-key", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;

        response.EnsureSuccessStatusCode();
        return true;
    }
}