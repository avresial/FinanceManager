using Blazored.LocalStorage;
using FinanceManager.Core.Entities.Login;
using FinanceManager.Core.Repositories;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Repositories
{
    public class LoginRepository : ILoginRepository
    {
        private readonly ILocalStorageService _localStorageService;

        public LoginRepository(ILocalStorageService localStorageService)
        {
            _localStorageService = localStorageService;
        }

        async Task<User?> ILoginRepository.GetUser(string login, string password)
        {
            List<UserDto>? databaseUserDtos = await _localStorageService.GetItemAsync<List<UserDto>>("Users");
            if (databaseUserDtos is null) return null;

            List<UserDto> userDtos = new List<UserDto>();

            if (databaseUserDtos is not null)
                userDtos = databaseUserDtos;

            var foundUser = userDtos.FirstOrDefault(x => x.Login == login);
            if (foundUser is null) return null;

            return new User()
            {
                Login = foundUser.Login,
                Id = foundUser.Id,
            };
        }

        async Task<bool> ILoginRepository.AddUser(string login, string password)
        {
            List<UserDto>? databaseUserDtos = await _localStorageService.GetItemAsync<List<UserDto>>("Users");
            List<UserDto> userDtos = new List<UserDto>();

            if (databaseUserDtos is not null)
                userDtos = databaseUserDtos;

            if (userDtos.Any(x => x.Login == login))
                return false; // maybe throw exception?

            userDtos.Add(new UserDto()
            {
                Login = login,
                Password = password,
                Id = userDtos.Count
            });

            try
            {
                await _localStorageService.SetItemAsync("Users", userDtos);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }

            return true;
        }

        async Task<bool> ILoginRepository.RemoveUser(int userId)
        {
            List<UserDto>? databaseUserDtos = await _localStorageService.GetItemAsync<List<UserDto>>("Users");
            if (databaseUserDtos is null) return false;

            List<UserDto> userDtos = new List<UserDto>();

            if (databaseUserDtos is not null)
                userDtos = databaseUserDtos;

            userDtos.RemoveAll(x => x.Id == userId);

            try
            {
                await _localStorageService.SetItemAsync("Users", userDtos);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }

            return true;
        }
    }
}
