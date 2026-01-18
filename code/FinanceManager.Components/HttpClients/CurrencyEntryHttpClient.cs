using FinanceManager.Domain.Commands.Account;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class CurrencyEntryHttpClient(HttpClient httpClient)
{
    public async Task<CurrencyAccountEntry?> GetEntry(int accountId, int entryId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankEntry?accountId={accountId}&entryId={entryId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            return await response.Content.ReadFromJsonAsync<CurrencyAccountEntry?>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankEntry/Oldest/{accountId}");
            return await response.Content.ReadFromJsonAsync<DateTime?>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankEntry/Youngest/{accountId}");
            return await response.Content.ReadFromJsonAsync<DateTime?>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> AddEntryAsync(AddCurrencyAccountEntry addEntry)
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

    public async Task<bool> UpdateEntryAsync(CurrencyAccountEntry entry)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/BankEntry", entry);
        return response.IsSuccessStatusCode;
    }
}