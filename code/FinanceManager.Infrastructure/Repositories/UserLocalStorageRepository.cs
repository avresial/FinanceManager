using Blazored.LocalStorage;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Repositories;

public class UserLocalStorageRepository(ILocalStorageService localStorageService) : IUserRepository
{
    async Task<User?> IUserRepository.GetUser(string login, string password)
    {
        login = login.ToLower();
        var databaseUserDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return null;

        var userDtos = databaseUserDtos is not null ? databaseUserDtos : [];

        var foundUser = userDtos.FirstOrDefault(x => x.Login == login);
        if (foundUser is null) return null;
        if (foundUser.Password != password) return null;

        return new()
        {
            Login = foundUser.Login,
            UserId = foundUser.Id,
            PricingLevel = foundUser.PricingLevel,
            CreationDate = foundUser.CreationDate,
        };
    }
    public async Task<User?> GetUser(int id)
    {
        var databaseUserDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return null;

        var userDtos = databaseUserDtos is not null ? databaseUserDtos : [];

        var foundUser = userDtos.FirstOrDefault(x => x.Id == id);
        if (foundUser is null) return null;

        return new()
        {
            Login = foundUser.Login,
            UserId = foundUser.Id,
            PricingLevel = foundUser.PricingLevel,
            CreationDate = foundUser.CreationDate,
        };
    }
    async Task<bool> IUserRepository.AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole)
    {
        login = login.ToLower();
        var databaseUserDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users");
        var userDtos = databaseUserDtos is not null ? databaseUserDtos : [];

        if (userDtos.Any(x => x.Login == login))
            return false; // maybe throw exception?

        userDtos.Add(new()
        {
            Login = login,
            Password = password,
            Id = userDtos.Any() ? userDtos.Max(x => x.Id) + 1 : 0,
            PricingLevel = pricingLevel,
            UserRole = userRole,
            CreationDate = DateTime.Now,
        });

        try
        {
            await localStorageService.SetItemAsync("Users", userDtos);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }

        return true;
    }

    async Task<bool> IUserRepository.RemoveUser(int userId)
    {
        var databaseUserDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return false;

        var userDtos = databaseUserDtos is not null ? databaseUserDtos : [];

        userDtos.RemoveAll(x => x.Id == userId);

        try
        {
            await localStorageService.SetItemAsync("Users", userDtos);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }

        return true;
    }

    public Task<bool> UpdatePassword(int userId, string password) => throw new NotImplementedException();
    public Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel) => throw new NotImplementedException();
    public Task<User?> GetUser(string login) => throw new NotImplementedException();
    public Task<int> GetUsersCount() => throw new NotImplementedException();
    public IAsyncEnumerable<User> GetUsers(DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public IAsyncEnumerable<User> GetUsers(int recordIndex, int recordsCount) => throw new NotImplementedException();
}
