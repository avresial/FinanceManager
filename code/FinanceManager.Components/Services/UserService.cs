using FinanceManager.Application.Commands.User;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class UserService(HttpClient httpClient, ILogger<UserService> logger) : IUserService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<UserService> _logger = logger;

    public event Action<User>? OnUserChangeEvent;

    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel)
    {
        AddUser addUserCommand = new AddUser(login, password, pricingLevel);

        try
        {
            var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/User/Add",
                 JsonHelper.GenerateStringContent(JsonHelper.SerializeObj(addUserCommand)));
            var result = await response.Content.ReadFromJsonAsync<bool>();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding user {login}", login);
        }
        return false;
    }
    public async Task<User?> GetUser(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<User>($"{_httpClient.BaseAddress}api/User/Get/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user {id}", id);
        }

        return null;
    }
    public async Task<RecordCapacity?> GetRecordCapacity(int userId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RecordCapacity>($"{_httpClient.BaseAddress}api/User/GetRecordCapacity/{userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user record capacity {userId}", userId);
        }

        return null;
    }
    public async Task<bool> Delete(int userId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_httpClient.BaseAddress}api/User/Delete/{userId}");
            if (response.StatusCode == System.Net.HttpStatusCode.OK) return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing user {userId}", userId);
        }

        return false;
    }
    public async Task<bool> UpdatePassword(int userId, string newPassword)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;

            UpdatePassword updatePasswordCommand = new UpdatePassword(userId, newPassword);
            var response = await _httpClient.PutAsJsonAsync($"{_httpClient.BaseAddress}api/User/UpdatePassword/", updatePasswordCommand);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                OnUserChangeEvent?.Invoke(existingUser);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing user {userId}", userId);
        }
        return false;
    }
    public async Task<bool> UpdatePricingPlan(int userId, PricingLevel newPricingLevel)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;

            UpdatePricingPlan updatePricingPlan = new UpdatePricingPlan(userId, newPricingLevel);
            var response = await _httpClient.PutAsJsonAsync($"{_httpClient.BaseAddress}api/User/UpdatePricingPlan/", updatePricingPlan);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                existingUser.PricingLevel = newPricingLevel;
                OnUserChangeEvent?.Invoke(existingUser);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing user {userId}", userId);
        }

        return false;
    }
}