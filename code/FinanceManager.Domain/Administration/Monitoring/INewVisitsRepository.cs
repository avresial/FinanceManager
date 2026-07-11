namespace FinanceManager.Domain.Administration.Monitoring;

public interface INewVisitsRepository
{
    Task<int> GetVisitAsync(DateTime visitDate);
    Task<bool> AddVisitAsync(DateTime visitDate);
}