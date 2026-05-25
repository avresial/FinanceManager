using FinanceManager.Application.Commands.User;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class UserService(UserHttpClient httpClient, ILogger<UserService> logger) : IUserService
{
    public event Action<User>? OnUserChangeEvent;

    public Task<bool> AddUser(string login, string password, PricingLevel pricingLevel)
    {
        try
        {
            return httpClient.AddUser(new AddUser(login, password, pricingLevel));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error adding user {login}", login);
        }
        return Task.FromResult(false);
    }
    public Task<User?> GetUser(int id)
    {
        try
        {
            return httpClient.GetUser(id);
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
            httpClient.GetRecordCapacity(userId);
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
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;

            if (await httpClient.Delete(userId))
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
    public async Task<bool> UpdatePassword(int userId, string newPassword)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;
            if (await httpClient.UpdatePassword(new(userId, newPassword)))
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
            if (await httpClient.UpdatePricingPlan(new(userId, newPricingLevel)))
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

    public async Task<bool> UpdateRole(int userId, UserRole userRole)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;
            if (await httpClient.UpdateRole(new(userId, userRole)))
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