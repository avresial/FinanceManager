using FinanceManager.Application.Commands.Account;
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
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/CurrencyEntry?accountId={accountId}&entryId={entryId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
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
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/CurrencyEntry/Oldest/{accountId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
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
            var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/CurrencyEntry/Youngest/{accountId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await response.Content.ReadFromJsonAsync<DateTime?>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> AddEntryAsync(AddCurrencyAccountEntry addEntry)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyEntry", addEntry);
        if (response.IsSuccessStatusCode) return true;
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> DeleteEntryAsync(int accountId, int entryId)
    {
        var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/CurrencyEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(CurrencyAccountEntry entry)
    {
        var updateCommand = new UpdateCurrencyAccountEntry(
            entry.AccountId,
            entry.EntryId,
            entry.PostingDate,
            entry.Value,
            entry.ValueChange,
            entry.Description,
            entry.ContractorDetails,
            entry.Labels?.Select(l => new UpdateFiancialLabel(l.Id, l.Name)).ToList() ?? []
        );
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/CurrencyEntry", updateCommand);
        return response.IsSuccessStatusCode;
    }
}