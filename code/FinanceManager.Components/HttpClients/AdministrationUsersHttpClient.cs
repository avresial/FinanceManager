using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.User;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class AdministrationUsersHttpClient(HttpClient httpClient)
{
    public async Task<List<ChartEntryModel>> GetNewUsersDaily()
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<ChartEntryModel>>($"{httpClient.BaseAddress}api/AdministrationUsers/GetNewUsersDaily");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<ChartEntryModel>> GetDailyActiveUsers()
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<ChartEntryModel>>($"{httpClient.BaseAddress}api/AdministrationUsers/GetDailyActiveUsers");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<int> GetAccountsCount()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/AdministrationUsers/GetAccountsCount");
        }
        catch
        {
            return 0;
        }
    }

    public async Task<int?> GetTotalTrackedMoney()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<int?>($"{httpClient.BaseAddress}api/AdministrationUsers/GetTotalTrackedMoney");
        }
        catch
        {
            return null;
        }
    }

    public async Task<int> GetUsersCount()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/AdministrationUsers/GetUsersCount");
        }
        catch
        {
            return 0;
        }
    }

    public async Task<List<UserDetails>> GetUsers(int recordIndex, int recordsCount)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<UserDetails>>($"{httpClient.BaseAddress}api/AdministrationUsers/GetUsers/{recordIndex}/{recordsCount}");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }
}