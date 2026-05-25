namespace FinanceManager.Domain.Repositories;

public interface INewVisitsRepository
{
    Task<int> GetVisitAsync(DateTime visitDate);
    Task<bool> AddVisitAsync(DateTime visitDate);
}