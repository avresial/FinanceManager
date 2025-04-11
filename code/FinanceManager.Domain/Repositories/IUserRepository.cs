﻿using FinanceManager.Domain.Entities.Login;

namespace FinanceManager.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetUser(string login, string password);
    Task<bool> AddUser(string login, string password);
    Task<bool> RemoveUser(int userId);
}
