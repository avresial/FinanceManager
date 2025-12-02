using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Bonds;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class BondEntryHttpClient(HttpClient httpClient)
{
    public async Task<BondAccountEntry?> GetEntry(int accountId, int entryId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BondEntry?accountId={accountId}&entryId={entryId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<BondAccountEntry?>();
    }

    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BondEntry/Oldest/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BondEntry/Youngest/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<bool> AddEntryAsync(AddBondAccountEntry addEntry)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BondEntry", addEntry);
        if (response.IsSuccessStatusCode) return true;
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeleteEntryAsync(int accountId, int entryId)
    {
        var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/BondEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(UpdateBondAccountEntry entry)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/BondEntry", entry);
        return response.IsSuccessStatusCode;
    }
}