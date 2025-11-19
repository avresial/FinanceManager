using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class BankAccountHttpClient(HttpClient httpClient)
{
    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccountsAsync()
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankAccount");
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<AvailableAccount>>();
        return result ?? Enumerable.Empty<AvailableAccount>();
    }

    public async Task<BankAccount?> GetAccountAsync(int accountId)
    {
        return await httpClient.GetFromJsonAsync<BankAccount>($"{httpClient.BaseAddress}api/BankAccount/{accountId}");
    }

    public async Task<BankAccount?> GetAccountWithEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        var result = await httpClient.GetFromJsonAsync<BankAccountDto>($"{httpClient.BaseAddress}api/BankAccount/{accountId}&{startDate:O}&{endDate:O}");
        if (result is null) return null;

        BankAccountEntry? nextOlderEntry = result.NextOlderEntry is null ? null : new(result.NextOlderEntry.AccountId, result.NextOlderEntry.EntryId,
            result.NextOlderEntry.PostingDate, result.NextOlderEntry.Value, result.NextOlderEntry.ValueChange);

        BankAccountEntry? nextYoungerEntry = result.NextYoungerEntry is null ? null : new(result.NextYoungerEntry.AccountId, result.NextYoungerEntry.EntryId,
            result.NextYoungerEntry.PostingDate, result.NextYoungerEntry.Value, result.NextYoungerEntry.ValueChange);

        return new(result.UserId, result.AccountId, result.Name, result.Entries.Select(x => new BankAccountEntry(x.AccountId, x.EntryId, x.PostingDate, x.Value, x.ValueChange)
        {
            Description = x.Description,
            Labels = x.Labels
        }), result.AccountLabel, nextOlderEntry, nextYoungerEntry);
    }

    public async Task<BankAccountEntry?> GetEntry(int accountId, int entryId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankAccount/GetEntry?accountId={accountId}&entryId={entryId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        var result = await response.Content.ReadFromJsonAsync<BankAccountEntry?>();
        return result;
    }

    public async Task<DateTime?> GetOldestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankAccount/GetOldestEntryDate/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<DateTime?> GetYoungestEntryDate(int accountId)
    {
        var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/BankAccount/GetYoungestEntryDate/{accountId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<DateTime?>();
    }

    public async Task<int?> AddAccountAsync(AddAccount addAccount)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BankAccount/Add", addAccount);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<int?>();
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> AddEntryAsync(AddBankAccountEntry addEntry)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BankAccount/AddEntry", addEntry);
        if (response.IsSuccessStatusCode) return true;
        throw new Exception(await response.Content.ReadAsStringAsync());
    }

    public async Task<bool> UpdateAccountAsync(UpdateAccount updateAccount)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/BankAccount/Update", updateAccount);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAccountAsync(int accountId)
    {
        return await httpClient.DeleteFromJsonAsync<bool>($"{httpClient.BaseAddress}api/BankAccount/Delete/{accountId}");
    }

    public async Task<bool> DeleteEntryAsync(int accountId, int entryId)
    {
        var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/BankAccount/DeleteEntry/{accountId}/{entryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateEntryAsync(BankAccountEntry entry)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/BankAccount/UpdateEntry", entry);
        return response.IsSuccessStatusCode;
    }

    public async Task<ImportResult?> ImportBankEntriesAsync(BankDataImportDto importDto)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BankAccount/ImportBankEntries", importDto);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<ImportResult?>();
    }

    public async Task<bool> ResolveImportConflictsAsync(IEnumerable<ResolvedImportConflict> resolvedConflicts)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BankAccount/ResolveImportConflicts", resolvedConflicts);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return true;
    }
}
