
namespace FinanceManager.Infrastructure.Repositories;

public interface IActiveUsersRepository1
{
    Task Add(int userId, DateOnly dateOnly);
}