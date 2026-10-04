using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared.Charting;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Administration.HttpClients;

public class AdministrationUsersHttpClient(HttpClient httpClient)
{
    public async Task<List<ChartEntryModel>> GetNewUsersDaily()
    {
        // Failures must reach the coordinator so they cannot overwrite a painted snapshot with an empty series.
        return await httpClient.GetFromJsonAsync<List<ChartEntryModel>>($"{httpClient.BaseAddress}api/AdministrationUsers/GetNewUsersDaily")
            ?? throw new InvalidOperationException("The new users response was empty.");
    }

    public async Task<List<ChartEntryModel>> GetDailyActiveUsers()
    {
        // Failures must reach the coordinator so they cannot overwrite a painted snapshot with an empty series.
        return await httpClient.GetFromJsonAsync<List<ChartEntryModel>>($"{httpClient.BaseAddress}api/AdministrationUsers/GetDailyActiveUsers")
            ?? throw new InvalidOperationException("The daily active users response was empty.");
    }

    public async Task<int?> GetAccountsCount()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/AdministrationUsers/GetAccountsCount");
        }
        catch
        {
            // Null means no usable response came back; a genuine zero is returned as data. The admin
            // dashboard snapshot surface relies on that distinction to keep stale content on failure.
            return null;
        }
    }

    public async Task<decimal?> GetTotalTrackedMoney()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<decimal?>($"{httpClient.BaseAddress}api/AdministrationUsers/GetTotalTrackedMoney");
        }
        catch
        {
            return null;
        }
    }

    public async Task<int?> GetUsersCount()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/AdministrationUsers/GetUsersCount");
        }
        catch
        {
            // Null means no usable response came back; a genuine zero is returned as data. The admin
            // dashboard snapshot surface relies on that distinction to keep stale content on failure.
            return null;
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