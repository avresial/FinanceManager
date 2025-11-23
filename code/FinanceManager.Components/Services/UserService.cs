using FinanceManager.Application.Commands.User;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class UserService(HttpClient httpClient, ILogger<UserService> logger) : IUserService
{
    public event Action<User>? OnUserChangeEvent;

    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel)
    {
        AddUser addUserCommand = new(login, password, pricingLevel);

        try
        {
            var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/User/Add",
                 JsonHelper.GenerateStringContent(JsonHelper.SerializeObj(addUserCommand)));
            var result = await response.Content.ReadFromJsonAsync<bool>();
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error adding user {login}", login);
        }
        return false;
    }
    public Task<User?> GetUser(int id)
    {
        try
        {
            return httpClient.GetFromJsonAsync<User>($"{httpClient.BaseAddress}api/User/Get/{id}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting user {id}", id);
        }
        return Task.FromResult((User?)null);
    }
    public Task<RecordCapacity?> GetRecordCapacity(int userId)
    {
        try
        {
            httpClient.GetFromJsonAsync<RecordCapacity>($"{httpClient.BaseAddress}api/User/GetRecordCapacity/{userId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting user record capacity {userId}", userId);
        }

        return Task.FromResult((RecordCapacity?)null); ;
    }
    public async Task<bool> Delete(int userId)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/User/Delete/{userId}");
            if (response.StatusCode == System.Net.HttpStatusCode.OK) return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error removing user {userId}", userId);
        }

        return false;
    }
    public async Task<bool> UpdatePassword(int userId, string newPassword)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;

            UpdatePassword updatePasswordCommand = new(userId, newPassword);
            var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/User/UpdatePassword/", updatePasswordCommand);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                OnUserChangeEvent?.Invoke(existingUser);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error removing user {userId}", userId);
        }
        return false;
    }
    public async Task<bool> UpdatePricingPlan(int userId, PricingLevel newPricingLevel)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;

            UpdatePricingPlan updatePricingPlan = new(userId, newPricingLevel);
            var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/User/UpdatePricingPlan/", updatePricingPlan);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                existingUser.PricingLevel = newPricingLevel;
                OnUserChangeEvent?.Invoke(existingUser);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error removing user {userId}", userId);
        }

        return false;
    }

    public async Task<bool> UpdateRole(int userId, UserRole userRole)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;

            UpdateUserRole updateUserRole = new(userId, userRole);
            var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/User/UpdateUserRole/", updateUserRole);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                OnUserChangeEvent?.Invoke(existingUser);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error updating role for user {userId}", userId);
        }

        return false;
    }
}