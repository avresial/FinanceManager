using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.User;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;
public class AdministrationUsersService(HttpClient httpClient, ILogger<AdministrationUsersService> logger) : IAdministrationUsersService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AdministrationUsersService> _logger = logger;

    public async Task<IEnumerable<ChartEntryModel>> GetNewUsersDaily()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<IEnumerable<ChartEntryModel>>($"{_httpClient.BaseAddress}api/AdministrationUsers/GetNewUsersDaily");
            if (result is null) return [];
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting New Users Daily");
            return [];
        }
    }
    public async Task<IEnumerable<ChartEntryModel>> GetDailyActiveUsers()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<IEnumerable<ChartEntryModel>>($"{_httpClient.BaseAddress}api/AdministrationUsers/GetDailyActiveUsers");
            if (result is null) return [];
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error Daily Active Users");
            return [];
        }
    }

    public async Task<int> GetAccountsCount()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<int>($"{_httpClient.BaseAddress}api/AdministrationUsers/GetAccountsCount");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting accounts count");
            return 0;
        }
    }
    public async Task<int?> GetTotalTrackedMoney()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<int?>($"{_httpClient.BaseAddress}api/AdministrationUsers/GetTotalTrackedMoney");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting Total Tracked Money");
            return null;
        }
    }
    public async Task<int> GetUsersCount()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<int>($"{_httpClient.BaseAddress}api/AdministrationUsers/GetUsersCount");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user count");
        }

        return 0;
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