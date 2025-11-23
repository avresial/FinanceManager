using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Cash;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class BankEntryHttpClient(HttpClient httpClient)
{
    public async Task<BankAccountEntry?> GetEntry(int accountId, int entryId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankEntry?accountId={accountId}&entryId={entryId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<BankAccountEntry?>();
    }

    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankEntry/Oldest/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankEntry/Youngest/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<bool> AddEntryAsync(AddBankAccountEntry addEntry)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BankEntry", addEntry);
        if (response.IsSuccessStatusCode) return true;
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeleteEntryAsync(int accountId, int entryId)
    {
        var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/BankEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(BankAccountEntry entry)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/BankEntry", entry);
        return response.IsSuccessStatusCode;
    }
}
