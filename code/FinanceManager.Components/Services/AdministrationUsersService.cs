using FinanceManager.Domain.Entities.User;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;
public class AdministrationUsersService(HttpClient httpClient, ILogger<AdministrationUsersService> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AdministrationUsersService> _logger = logger;

    public async Task<int?> GetAccountsCount()
    {
        int? result = null;
        return await Task.FromResult(result);
    }

    public async Task<int?> GetTotalTrackedMoney()
    {
        int? result = null;
        return await Task.FromResult(result);
    }
    public async Task<int?> GetUsersCount()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<int>($"{_httpClient.BaseAddress}api/AdministrationUsers/GetUsersCount");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user count");
        }

        return null;
    }
    public async Task<IEnumerable<UserDetails>> GetUsers(int recordIndex, int recordsCount)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<IEnumerable<UserDetails>>($"{_httpClient.BaseAddress}api/AdministrationUsers/GetUsers/{recordIndex}/{recordsCount}");

            if (result is null) return [];
            else return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting users");
        }

        return null;
    }
}



