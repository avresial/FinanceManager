using FinanceManager.Components.Features.Identity.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Commands;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Identity.Services;

public class UserService(UserHttpClient httpClient, ILogger<UserService> logger) : IUserService
{
    public event Action<User>? OnUserChangeEvent;

    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, string? firstName = null, string? lastName = null)
    {
        try
        {
            return await httpClient.AddUser(new AddUser(login, password, pricingLevel, firstName, lastName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error adding user {login}", login);
        }
        return false;
    }
    public async Task<User?> GetUser(int id)
    {
        try
        {
            // Must await here: returning the un-awaited task let the async 401 (e.g. an expired token) escape this
            // try/catch and surface as an unhandled exception in callers such as MainLayout.OnInitializedAsync.
            return await httpClient.GetUser(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting user {id}", id);
        }
        return null;
    }
    public async Task<RecordCapacity?> GetRecordCapacity(int userId)
    {
        try
        {
            return await httpClient.GetRecordCapacity(userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting user record capacity {userId}", userId);
        }

        return null;
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
    public async Task<bool> UpdatePassword(int userId, string newPassword, string? currentPassword = null)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;
            if (await httpClient.UpdatePassword(new(userId, newPassword, currentPassword)))
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

    public async Task<bool> UpdatePreferredCurrency(int userId, int currencyId)
    {
        try
        {
            var existingUser = await GetUser(userId);
            if (existingUser is null) return false;
            if (await httpClient.UpdatePreferredCurrency(new(userId, currencyId)))
            {
                OnUserChangeEvent?.Invoke(existingUser);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating preferred currency for user {UserId}", userId);
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